using AemulusModManager.Avalonia.Utilities;
using AemulusModManager.Avalonia.Views;
using AemulusModManager.Utilities.PackageUpdating;
using AemulusModManager.Utilities.PackageUpdating.DownloadUtils;
using Newtonsoft.Json;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using AemulusModManager.Avalonia.Converters;

namespace AemulusModManager.Avalonia;

public class PackageUpdater {
    private readonly HttpClient client;
    private readonly GitHubClient gitHubClient;
    private readonly DialogService _dialogService;
    private UpdateProgressBox? _progressBox;
    private string assemblyLocation;
    private string dataLocation;

    public PackageUpdater(DialogService dialogService) {
        client = new HttpClient();
        gitHubClient = new GitHubClient(new ProductHeaderValue("Aemulus"));
        _dialogService = dialogService;
        assemblyLocation = Utilities.AppPaths.ExeDir;
        dataLocation = Utilities.AppPaths.DataDir;
    }

    private string ConvertUrl(string oldUrl) {
        try {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var httpClient = new HttpClient(handler);
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var request = new HttpRequestMessage(HttpMethod.Head, oldUrl);
            var response = httpClient.Send(request);
            if ((int)response.StatusCode >= 300 && (int)response.StatusCode <= 399) {
                var location = response.Headers.Location?.ToString();
                return location ?? oldUrl;
            }
            return oldUrl;
        }
        catch {
            return oldUrl;
        }
    }

    public async Task<bool> CheckForUpdate(DisplayedMetadata[] rows, string game, CancellationTokenSource cancellationToken, bool downloadingMissing = false) {
        var updated = false;
        try {
            DisplayedMetadata[] gameBananaRows = rows.Where(row => Converters.UrlConverter.ConvertUrl(row.link) == "GameBanana").ToArray();
            if (gameBananaRows.Length > 0) {
                var requestUrls = new Dictionary<string, List<string>>();
                var urlCounts = new Dictionary<string, int>();
                var modList = new Dictionary<string, List<DisplayedMetadata>>();
                foreach (var row in gameBananaRows) {
                    Uri uri = CreateUri(row.link);
                    if (uri == null) continue;
                    string MOD_TYPE = uri.Segments[1];
                    MOD_TYPE = char.ToUpper(MOD_TYPE[0]) + MOD_TYPE.Substring(1, MOD_TYPE.Length - 3);
                    string MOD_ID = uri.Segments[2];
                    switch (MOD_TYPE) {
                        case "Gamefile":
                        case "Skin":
                        case "Gui":
                        case "Texture":
                        case "Effect":
                            var newUrl = ConvertUrl(row.link);
                            uri = CreateUri(newUrl);
                            if (uri == null) continue;
                            MOD_TYPE = uri.Segments[1];
                            MOD_TYPE = char.ToUpper(MOD_TYPE[0]) + MOD_TYPE.Substring(1, MOD_TYPE.Length - 3);
                            MOD_ID = uri.Segments[2];
                            break;
                    }
                    if (!urlCounts.ContainsKey(MOD_TYPE))
                        urlCounts.Add(MOD_TYPE, 0);
                    int index = urlCounts[MOD_TYPE];
                    if (!modList.ContainsKey(MOD_TYPE))
                        modList.Add(MOD_TYPE, new List<DisplayedMetadata>());
                    modList[MOD_TYPE].Add(row);
                    if (!requestUrls.ContainsKey(MOD_TYPE))
                        requestUrls.Add(MOD_TYPE, new string[] { $"https://gamebanana.com/apiv6/{MOD_TYPE}/Multi?_csvProperties=_aModManagerIntegrations,_sName,_bHasUpdates,_aLatestUpdates,_aFiles,_aPreviewMedia,_aAlternateFileSources&_csvRowIds=" }.ToList());
                    else if (requestUrls[MOD_TYPE].Count == index)
                        requestUrls[MOD_TYPE].Add($"https://gamebanana.com/apiv6/{MOD_TYPE}/Multi?_csvProperties=_aModManagerIntegrations,_sName,_bHasUpdates,_aLatestUpdates,_aFiles,_aPreviewMedia,_aAlternateFileSources&_csvRowIds=");
                    requestUrls[MOD_TYPE][index] += $"{MOD_ID},";
                    if (requestUrls[MOD_TYPE][index].Length > 1990)
                        urlCounts[MOD_TYPE]++;
                }
                foreach (var key in requestUrls.Keys) {
                    var counter = 0;
                    foreach (var requestUrl in requestUrls[key].ToList()) {
                        if (requestUrl.EndsWith(","))
                            requestUrls[key][counter] = requestUrl.Substring(0, requestUrl.Length - 1);
                        counter++;
                    }
                }
                List<GameBananaAPIV4> response = new List<GameBananaAPIV4>();
                using (var httpClient = new HttpClient()) {
                    foreach (var type in requestUrls) {
                        foreach (var requestUrl in type.Value) {
                            var responseString = await httpClient.GetStringAsync(requestUrl);
                            try {
                                var partialResponse = JsonConvert.DeserializeObject<List<GameBananaAPIV4>>(responseString.Replace("\"_aModManagerIntegrations\": []", "\"_aModManagerIntegrations\": {}"));
                                if (partialResponse != null)
                                    response = response.Concat(partialResponse).ToList();
                            }
                            catch (Exception e) {
                                AemulusModManager.Utilities.ParallelLogger.Log($"[ERROR] {e.Message}");
                            }
                        }
                    }
                }
                if (response == null) {
                    AemulusModManager.Utilities.ParallelLogger.Log("[ERROR] Error whilst checking for package updates: No response from GameBanana API");
                }
                else {
                    var convertedModList = new List<DisplayedMetadata>();
                    foreach (var type in modList) {
                        foreach (var mod in type.Value) {
                            convertedModList.Add(mod);
                        }
                    }
                    for (int i = 0; i < convertedModList.Count; i++) {
                        try {
                            if (await GameBananaUpdate(response[i], convertedModList[i], game, new Progress<DownloadProgress>(ReportUpdateProgress), CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token), downloadingMissing))
                                updated = true;
                        }
                        catch (Exception e) {
                            AemulusModManager.Utilities.ParallelLogger.Log($"[ERROR] Error whilst updating/checking for updates for {convertedModList[i].name}: {e.Message}");
                        }
                    }
                }
            }
            DisplayedMetadata[] gitHubRows = rows.Where(row => Converters.UrlConverter.ConvertUrl(row.link) == "GitHub").ToArray();
            if (gitHubRows.Length > 0) {
                foreach (DisplayedMetadata row in gitHubRows) {
                    try {
                        Uri uri = CreateUri(row.link);
                        if (uri == null) continue;
                        Release latestRelease = await gitHubClient.Repository.Release.GetLatest(uri.Segments[1].Replace("/", ""), uri.Segments[2].Replace("/", ""));
                        if (await GitHubUpdate(latestRelease, row, game, new Progress<DownloadProgress>(ReportUpdateProgress), cancellationToken, downloadingMissing))
                            updated = true;
                    }
                    catch (Exception e) {
                        AemulusModManager.Utilities.ParallelLogger.Log($"[ERROR] Error whilst updating/checking for updates for {row.name}: {e.Message}");
                    }
                }
            }
        }
        catch (HttpRequestException e) {
            AemulusModManager.Utilities.ParallelLogger.Log($"[ERROR] Connection error whilst checking for updates: {e.Message}");
        }
        catch (Exception e) {
            AemulusModManager.Utilities.ParallelLogger.Log($"[ERROR] Error whilst checking for updates: {e.Message}");
        }
        return updated;
    }

    public async Task<bool> CheckForAemulusUpdate(string aemulusVersion, CancellationTokenSource cancellationToken) {
        try {
            Uri uri = CreateUri("https://github.com/TekkaGB/AemulusModManager");
            if (uri == null) return false;
            Release release = await gitHubClient.Repository.Release.GetLatest(uri.Segments[1].Replace("/", ""), uri.Segments[2].Replace("/", ""));
            Match onlineVersionMatch = Regex.Match(release.TagName, @"(?<version>([0-9]+\.?)+)[^a-zA-Z]*");
            string? onlineVersion = null;
            if (onlineVersionMatch.Success) {
                onlineVersion = onlineVersionMatch.Groups["version"].Value;
            }
            if (UpdateAvailable(onlineVersion, aemulusVersion)) {
                AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] An update is available for Aemulus ({onlineVersion})");
                var yes = await _dialogService.ShowNotification(
                    $"Aemulus has a new update ({release.TagName}):\n{release.Body}\n\nPlease download the latest version from GitHub.", true);
                // Self-update via Onova is Windows-only; on Linux, just notify the user
            }
            else {
                AemulusModManager.Utilities.ParallelLogger.Log("[INFO] No updates available for Aemulus");
            }
        }
        catch (Exception e) {
            AemulusModManager.Utilities.ParallelLogger.Log($"[ERROR] Error whilst checking for updates: {e.Message}");
        }
        return false;
    }

    private void ReportUpdateProgress(DownloadProgress progress) {
        if (_progressBox == null) return;
        _ = _dialogService.UpdateProgress(_progressBox, progress.Percentage,
            $"Downloading {progress.FileName}...",
            $"{Math.Round(progress.Percentage * 100, 2)}% " +
            $"({AemulusModManager.Utilities.PackageUpdating.StringConverters.FormatSize(progress.DownloadedBytes)} of {AemulusModManager.Utilities.PackageUpdating.StringConverters.FormatSize(progress.TotalBytes)})");
    }

    private async Task<bool> GameBananaUpdate(GameBananaAPIV4 item, DisplayedMetadata row, string game, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, bool downloadingMissing) {
        var updated = false;
        if (!downloadingMissing && item.HasUpdates != null && (bool)item.HasUpdates) {
            GameBananaItemUpdate[] updates = item.Updates;
            int updateIndex = 0;
            string? onlineVersion = ExtractVersion(updates, ref updateIndex);

            Match localVersionMatch = Regex.Match(row.version ?? "", @"(?<version>([0-9]+\.?)+)[^a-zA-Z]*");
            string? localVersion = localVersionMatch.Success ? localVersionMatch.Groups["version"].Value : null;

            if (row.skippedVersion != null) {
                if (row.skippedVersion == "all" || !UpdateAvailable(onlineVersion, row.skippedVersion)) {
                    AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] No updates available for {row.name}");
                    return false;
                }
            }
            if (UpdateAvailable(onlineVersion, localVersion)) {
                AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] An update is available for {row.name} ({onlineVersion})");
                var packageXmlPath = Path.Combine(dataLocation, "Packages", game, row.path ?? "", "Package.xml");
                var (yesNo, _) = await _dialogService.ShowChangelog(updates[updateIndex], row.name,
                    $"Would you like to update {row.name} to version {onlineVersion}?",
                    row, onlineVersion ?? "", packageXmlPath, false);
                if (!yesNo) {
                    AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] Cancelled update for {row.name}");
                    return false;
                }
                await GameBananaDownload(item, row, game, progress, cancellationToken, downloadingMissing, updates, onlineVersion, updateIndex);
                updated = true;
            }
            else {
                AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] No updates available for {row.name}");
            }
        }
        else if (downloadingMissing) {
            var yes = await _dialogService.ShowDownloadConfirm(row.name, item.Owner?.Name, item.Image);
            if (yes) {
                await GameBananaDownload(item, row, game, progress, cancellationToken, downloadingMissing, null, null, 0);
            }
        }
        else {
            AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] No updates available for {row.name}");
        }
        return updated;
    }

    private string? ExtractVersion(GameBananaItemUpdate[] updates, ref int updateIndex) {
        string? onlineVersion = null;
        Match match;
        for (int i = 0; i < Math.Min(updates.Length, 2); i++) {
            if (updates[i].Version != null) {
                match = Regex.Match(updates[i].Version, @"(?<version>([0-9]+\.?)+)[^a-zA-Z]*");
                if (match.Success) { updateIndex = i; return match.Groups["version"].Value; }
            }
            match = Regex.Match(updates[i].Title ?? "", @"(?<version>([0-9]+\.?)+)[^a-zA-Z]*");
            if (match.Success) { updateIndex = i; return match.Groups["version"].Value; }
        }
        return onlineVersion;
    }

    private async Task GameBananaDownload(GameBananaAPIV4 item, DisplayedMetadata row, string game, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, bool downloadingMissing, GameBananaItemUpdate[]? updates, string? onlineVersion, int updateIndex) {
        string? downloadUrl = null;
        string? fileName = null;
        List<GameBananaItemFile> aemulusCompatibleFiles = item.Files.Where(x => item.ModManagerIntegrations.ContainsKey(x.ID)).ToList();
        if (aemulusCompatibleFiles.Count > 1) {
            var (url, name) = await _dialogService.ShowFileSelector(aemulusCompatibleFiles, row.name);
            downloadUrl = url;
            fileName = name;
        }
        else if (aemulusCompatibleFiles.Count == 1) {
            downloadUrl = aemulusCompatibleFiles[0].DownloadUrl;
            fileName = aemulusCompatibleFiles[0].FileName;
        }
        else if (!downloadingMissing) {
            AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] An update is available for {row.name} ({onlineVersion}) but there are no downloads directly from GameBanana.");
            await _dialogService.ShowAltLinks(item.AlternateFileSources, row.name, game, true);
            return;
        }
        if (downloadUrl != null && fileName != null) {
            await DownloadFile(downloadUrl, fileName, game, row, onlineVersion, progress, cancellationToken, downloadingMissing);
        }
        else {
            AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] Cancelled update for {row.name}");
        }
    }

    private async Task<bool> GitHubUpdate(Release release, DisplayedMetadata row, string game, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, bool downloadingMissing) {
        var updated = false;
        if (downloadingMissing) {
            var yes = await _dialogService.ShowDownloadConfirm(row.name, row.author);
            if (yes) {
                await GithubDownload(release, row, game, progress, cancellationToken, downloadingMissing);
            }
        }
        else {
            Match onlineVersionMatch = Regex.Match(release.TagName, @"(?<version>([0-9]+\.?)+)[^a-zA-Z]*");
            string? onlineVersion = onlineVersionMatch.Success ? onlineVersionMatch.Groups["version"].Value : null;

            Match localVersionMatch = Regex.Match(row.version ?? "", @"(?<version>([0-9]+\.?)+)[^a-zA-Z]*");
            string? localVersion = localVersionMatch.Success ? localVersionMatch.Groups["version"].Value : null;

            if (row.skippedVersion != null) {
                if (row.skippedVersion == "all" || !UpdateAvailable(onlineVersion, row.skippedVersion)) {
                    AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] No updates available for {row.name}");
                    return false;
                }
            }
            if (UpdateAvailable(onlineVersion, localVersion)) {
                AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] An update is available for {row.name} ({release.TagName})");
                var yes = await _dialogService.ShowNotification(
                    $"{row.name} has an update ({release.TagName}):\n{release.Body}\n\nWould you like to update?", false);
                if (!yes) return false;
                await GithubDownload(release, row, game, progress, cancellationToken, downloadingMissing);
                updated = true;
            }
            else {
                AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] No updates available for {row.name}");
            }
        }
        return updated;
    }

    private async Task GithubDownload(Release release, DisplayedMetadata row, string game, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, bool downloadingMissing) {
        string? downloadUrl, fileName;
        if (release.Assets.Count > 1) {
            var (url, name) = await _dialogService.ShowFileSelector(release.Assets, row.name);
            downloadUrl = url;
            fileName = name;
        }
        else if (release.Assets.Count == 1) {
            downloadUrl = release.Assets.First().BrowserDownloadUrl;
            fileName = release.Assets.First().Name;
        }
        else {
            AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] An update is available for {row.name} ({release.TagName}) but no downloadable files are available.");
            var yes = await _dialogService.ShowNotification(
                $"{row.name} has an update ({release.TagName}) but no downloadable files.\nWould you like to go to the page to manually download the update?", false);
            if (yes) {
                Process.Start(new ProcessStartInfo(row.link) { UseShellExecute = true });
            }
            return;
        }
        if (downloadUrl != null && fileName != null) {
            await DownloadFile(downloadUrl, fileName, game, row, release.TagName, progress, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token), downloadingMissing);
        }
        else {
            AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] Cancelled update for {row.name}");
        }
    }

    private async Task DownloadFile(string uri, string fileName, string game, DisplayedMetadata row, string? version, Progress<DownloadProgress> progress, CancellationTokenSource cancellationToken, bool downloadingMissing) {
        try {
            var downloadsDir = Path.Combine(dataLocation, "Downloads");
            Directory.CreateDirectory(downloadsDir);
            var filePath = Path.Combine(downloadsDir, fileName);
            if (!File.Exists(filePath)) {
                _progressBox = _dialogService.CreateProgressBox(cancellationToken);
                await _dialogService.UpdateProgress(_progressBox, 0, $"Downloading {fileName}", "");
                _progressBox.Title = $"{row.name} {(downloadingMissing ? "Download" : "Update")} Progress";
                await _dialogService.ShowProgressBox(_progressBox);
                AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] Downloading {fileName}");
                using (var fs = new FileStream(filePath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None)) {
                    await client.DownloadAsync(uri, fs, fileName, progress, cancellationToken.Token);
                }
                AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] Finished downloading {fileName}");
                await _dialogService.CloseProgressBox(_progressBox);
            }
            else {
                AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] {fileName} already exists in downloads, using this instead");
            }
            ExtractFile(fileName, game);
        }
        catch (OperationCanceledException) {
            var filePath = Path.Combine(dataLocation, "Downloads", fileName);
            if (File.Exists(filePath)) File.Delete(filePath);
            if (_progressBox != null)
                await _dialogService.CloseProgressBox(_progressBox);
        }
        catch (Exception e) {
            AemulusModManager.Utilities.ParallelLogger.Log($"[ERROR] Error whilst downloading {fileName}: {e.Message}");
            if (_progressBox != null)
                await _dialogService.CloseProgressBox(_progressBox);
        }
    }

    private void ExtractFile(string file, string game) {
        var ext = Path.GetExtension(file).ToLower();
        if (ext == ".7z" || ext == ".rar" || ext == ".zip") {
            var tempDir = Path.Combine(dataLocation, "temp", Path.GetFileNameWithoutExtension(file));
            Directory.CreateDirectory(tempDir);

            // Use 7z command (cross-platform)
            var sevenZipPath = FindSevenZip();
            if (sevenZipPath == null) {
                AemulusModManager.Utilities.ParallelLogger.Log("[ERROR] Couldn't find 7z. Please install p7zip or 7zip.");
                return;
            }

            var filePath = Path.Combine(dataLocation, "Downloads", file);
            var startInfo = new ProcessStartInfo {
                CreateNoWindow = true,
                FileName = sevenZipPath,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                Arguments = $"x -y \"{filePath}\" -o\"{tempDir}\""
            };
            using (var process = Process.Start(startInfo)) {
                process?.WaitForExit();
            }

            var tempBase = Path.Combine(dataLocation, "temp");
            foreach (var folder in Directory.GetDirectories(tempBase, "*", SearchOption.AllDirectories)
                .Where(x => File.Exists(Path.Combine(x, "Package.xml")) || File.Exists(Path.Combine(x, "Mod.xml")))) {
                string path = Path.Combine(dataLocation, "Packages", game, Path.GetFileName(folder));
                int index = 2;
                while (Directory.Exists(path)) {
                    path = Path.Combine(dataLocation, "Packages", game, $"{Path.GetFileName(folder)} ({index})");
                    index++;
                }
                MoveDirectory(folder, path);
            }
            var packageSetup = Directory.GetFiles(tempBase, "*.xml", SearchOption.AllDirectories)
                .Where(xml => !Path.GetFileName(xml).Equals("Package.xml", StringComparison.InvariantCultureIgnoreCase) &&
                              !Path.GetFileName(xml).Equals("Mod.xml", StringComparison.InvariantCultureIgnoreCase)).ToList();
            if (packageSetup.Count > 0) {
                var configTemp = Path.Combine(dataLocation, "Config", "temp");
                Directory.CreateDirectory(configTemp);
                foreach (var xml in packageSetup) {
                    File.Copy(xml, Path.Combine(configTemp, Path.GetFileName(xml)), true);
                }
            }
            File.Delete(filePath);
        }
        else {
            AemulusModManager.Utilities.ParallelLogger.Log($"[ERROR] {file} isn't a .zip, .7z, or .rar, couldn't extract...");
        }
        var tempPath = Path.Combine(dataLocation, "temp");
        if (Directory.Exists(tempPath))
            Directory.Delete(tempPath, true);
    }

    private string? FindSevenZip() {
        // Check for bundled 7z first
        var bundled = Path.Combine(assemblyLocation, "Dependencies", "7z", "7z");
        if (File.Exists(bundled)) return bundled;
        bundled += ".exe";
        if (File.Exists(bundled)) return bundled;

        // Check system PATH
        try {
            var psi = new ProcessStartInfo("7z", "--help") {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            if (p?.ExitCode == 0) return "7z";
        }
        catch { }

        // Try 7za (p7zip)
        try {
            var psi = new ProcessStartInfo("7za", "--help") {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            if (p?.ExitCode == 0) return "7za";
        }
        catch { }

        return null;
    }

    private void MoveDirectory(string source, string target) {
        var sourcePath = source.TrimEnd(Path.DirectorySeparatorChar, ' ');
        var targetPath = target.TrimEnd(Path.DirectorySeparatorChar, ' ');
        var files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories)
                             .GroupBy(s => Path.GetDirectoryName(s));
        foreach (var folder in files) {
            var targetFolder = folder.Key!.Replace(sourcePath, targetPath);
            Directory.CreateDirectory(targetFolder);
            foreach (var file in folder) {
                var targetFile = Path.Combine(targetFolder, Path.GetFileName(file));
                if (File.Exists(targetFile)) File.Delete(targetFile);
                File.Move(file, targetFile);
            }
        }
        Directory.Delete(source, true);
    }

    public bool UpdateAvailable(string? onlineVersion, string? localVersion) {
        if (onlineVersion is null || localVersion is null) return false;
        string[] onlineParts = onlineVersion.Split('.');
        string[] localParts = localVersion.Split('.');
        int maxLen = Math.Max(onlineParts.Length, localParts.Length);
        while (onlineParts.Length < maxLen) onlineParts = onlineParts.Append("0").ToArray();
        while (localParts.Length < maxLen) localParts = localParts.Append("0").ToArray();
        for (int i = 0; i < onlineParts.Length; i++) {
            if (!int.TryParse(onlineParts[i], out int online) || !int.TryParse(localParts[i], out int local))
                return false;
            if (online > local) return true;
            if (online != local) return false;
        }
        return false;
    }

    private Uri? CreateUri(string url) {
        if ((Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || Uri.TryCreate("http://" + url, UriKind.Absolute, out uri)) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
            return uri;
        }
        return null;
    }
}
