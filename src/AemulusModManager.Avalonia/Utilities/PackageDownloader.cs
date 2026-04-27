using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AemulusModManager.Avalonia.Utilities;
using AemulusModManager.Avalonia.Views;
using AemulusModManager.Utilities.PackageUpdating;
using AemulusModManager.Utilities.PackageUpdating.DownloadUtils;
using Avalonia.Threading;
using Newtonsoft.Json;

namespace AemulusModManager.Avalonia;

public class PackageDownloader {
    private string? URL_TO_ARCHIVE;
    private string? URL;
    private string? DL_ID;
    private string? fileName;
    private string assemblyLocation = Utilities.AppPaths.ExeDir;
    private string dataLocation = Utilities.AppPaths.DataDir;
    private bool cancelled;
    private HttpClient client = new HttpClient();
    private GameBananaAPIV4 response = new GameBananaAPIV4();
    private CancellationTokenSource cancellationToken = new CancellationTokenSource();
    private UpdateProgressBox? _progressBox;
    private readonly DialogService _dialogService;


    public PackageDownloader(DialogService dialogService) {
        _dialogService = dialogService;
        App.Ipc?.RegisterMessageHandler(DownloadTaskIPC);
    }

    public async Task DownloadTaskIPC(string line) {
        Dispatcher.UIThread.Post(async () => await Download(line, true));
    }

    public async Task BrowserDownload(GameBananaRecord record, string gameName) {
        var yes = await _dialogService.ShowDownloadConfirm(record);
        if (yes) {
            string? downloadUrl = null;
            string? downloadFileName = null;
            if (record.Files.Count == 0) {
                await _dialogService.ShowNotification("No Aemulus compatible files found for this mod.", true);
                return;
            }
            else if (record.Files.Count == 1) {
                downloadUrl = record.Files[0].DownloadUrl;
                downloadFileName = record.Files[0].FileName;
            }
            else if (record.Files.Count > 1) {
                var (url, name) = await _dialogService.ShowFileSelector(
                    record.Files.Cast<GameBananaItemFile>().ToList(), record.Title);
                downloadUrl = url;
                downloadFileName = name;
            }
            if (downloadUrl != null && downloadFileName != null) {
                await DownloadFile(downloadUrl, downloadFileName, new Progress<DownloadProgress>(ReportUpdateProgress),
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token));
                if (!cancelled) {
                    await ExtractFile(Path.Combine(dataLocation, "Downloads", downloadFileName), gameName);
                    var refreshPath = Path.Combine(dataLocation, "refresh.aem");
                    if (File.Exists(refreshPath)) File.Delete(refreshPath);
                    File.WriteAllText(refreshPath, gameName);
                }
            }
        }
    }

    public async Task Download(string line, bool running) {
        if (ParseProtocol(line)) {
            if (await GetData()) {
                var yes = await _dialogService.ShowDownloadConfirm(
                    response.Title ?? "Unknown", response.Owner?.Name, response.Image);
                if (yes) {
                    await DownloadFile(URL_TO_ARCHIVE!, fileName!, new Progress<DownloadProgress>(ReportUpdateProgress),
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Token));
                    var gameName = response.Game?.Name ?? "";
                    gameName = gameName switch {
                        "Persona 4 Golden PC (32 Bit)" => "Persona 4 Golden",
                        "Persona 3 Portable (PSP)" => "Persona 3 Portable",
                        "Shin Megami Tensei: Persona (PSP)" => "Persona 1 (PSP)",
                        "Persona 5 Royal" => "Persona 5 Royal (PS4)",
                        "Persona Q: Shadow of the Labryinth" => "Persona Q",
                        _ => gameName
                    };
                    await ExtractFile(Path.Combine(dataLocation, "Downloads", fileName!), gameName);
                    var refreshPath = Path.Combine(dataLocation, "refresh.aem");
                    if (File.Exists(refreshPath)) File.Delete(refreshPath);
                    File.WriteAllText(refreshPath, gameName);
                }
            }
        }
    }

    private async Task<bool> GetData() {
        try {
            string responseString = await client.GetStringAsync(URL);
            response = JsonConvert.DeserializeObject<GameBananaAPIV4>(responseString) ?? new GameBananaAPIV4();
            fileName = response.Files.Where(x => x.ID == DL_ID).ToArray()[0].FileName;
            return true;
        }
        catch (Exception e) {
            await _dialogService.ShowNotification($"Error while fetching data: {e.Message}", true);
            return false;
        }
    }

    private void ReportUpdateProgress(DownloadProgress progress) {
        if (_progressBox == null) return;
        _ = _dialogService.UpdateProgress(_progressBox, progress.Percentage,
            $"Downloading {progress.FileName}...",
            $"{Math.Round(progress.Percentage * 100, 2)}% " +
            $"({AemulusModManager.Utilities.PackageUpdating.StringConverters.FormatSize(progress.DownloadedBytes)} of {AemulusModManager.Utilities.PackageUpdating.StringConverters.FormatSize(progress.TotalBytes)})");
    }

    private bool ParseProtocol(string line) {
        try {
            line = line.Replace("aemulus:", "");
            string[] data = line.Split(',');
            URL_TO_ARCHIVE = data[0];
            var match = Regex.Match(URL_TO_ARCHIVE, @"\d*$");
            DL_ID = match.Value;
            string MOD_TYPE = data[1];
            string MOD_ID = data[2];
            URL = $"https://gamebanana.com/apiv6/{MOD_TYPE}/{MOD_ID}?_csvProperties=_sName,_aGame,_sProfileUrl,_aPreviewMedia,_sDescription,_aSubmitter,_aCategory,_aSuperCategory,_aFiles,_tsDateUpdated,_aAlternateFileSources,_bHasUpdates,_aLatestUpdates";
            return true;
        }
        catch (Exception e) {
            _ = _dialogService.ShowNotification($"Error while parsing {line}: {e.Message}", true);
            return false;
        }
    }

    private async Task DownloadFile(string uri, string downloadFileName, Progress<DownloadProgress> progress, CancellationTokenSource cts) {
        try {
            var downloadsDir = Path.Combine(dataLocation, "Downloads");
            Directory.CreateDirectory(downloadsDir);
            var filePath = Path.Combine(downloadsDir, downloadFileName);
            if (File.Exists(filePath)) {
                try { File.Delete(filePath); }
                catch (Exception e) {
                    await _dialogService.ShowNotification($"Couldn't delete existing {filePath} ({e.Message})", true);
                    return;
                }
            }
            _progressBox = _dialogService.CreateProgressBox(cts);
            await _dialogService.UpdateProgress(_progressBox, 0, "", $"Downloading {downloadFileName}");
            _progressBox.Title = "Update Progress";
            await _dialogService.ShowProgressBox(_progressBox);
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                await client.DownloadAsync(uri, fs, downloadFileName, progress, cts.Token);
            }
            await _dialogService.CloseProgressBox(_progressBox);
        }
        catch (OperationCanceledException) {
            var filePath = Path.Combine(dataLocation, "Downloads", downloadFileName);
            if (File.Exists(filePath)) File.Delete(filePath);
            if (_progressBox != null) {
                await _dialogService.CloseProgressBox(_progressBox);
                cancelled = true;
            }
        }
        catch (Exception e) {
            if (_progressBox != null) {
                await _dialogService.CloseProgressBox(_progressBox);
                cancelled = true;
            }
            await _dialogService.ShowNotification($"Error whilst downloading {downloadFileName}: {e.Message}", true);
        }
    }

    private async Task ExtractFile(string file, string game) {
        await Task.Run(() => {
            var ext = Path.GetExtension(file).ToLower();
            if (ext == ".7z" || ext == ".rar" || ext == ".zip") {
                var tempDir = Path.Combine(dataLocation, "temp", Path.GetFileNameWithoutExtension(file));
                Directory.CreateDirectory(tempDir);

                var sevenZipPath = FindSevenZip();
                if (sevenZipPath == null) {
                    AemulusModManager.Utilities.ParallelLogger.Log("[ERROR] Couldn't find 7z. Please install p7zip or 7zip.");
                    return;
                }

                var startInfo = new ProcessStartInfo {
                    CreateNoWindow = true,
                    FileName = sevenZipPath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    Arguments = $"x -y \"{file}\" -o\"{tempDir}\""
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
                File.Delete(file);
            }
            else {
                AemulusModManager.Utilities.ParallelLogger.Log($"[ERROR] {file} isn't a .zip, .7z, or .rar, couldn't extract...");
            }
            var tempPath = Path.Combine(dataLocation, "temp");
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);
        });
    }

    private string? FindSevenZip() {
        var bundled = Path.Combine(assemblyLocation, "Dependencies", "7z", "7z");
        if (File.Exists(bundled)) return bundled;
        bundled += ".exe";
        if (File.Exists(bundled)) return bundled;
        try {
            var psi = new ProcessStartInfo("7z", "--help") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            if (p?.ExitCode == 0) return "7z";
        }
        catch { }
        try {
            var psi = new ProcessStartInfo("7za", "--help") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
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
}
