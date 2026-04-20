using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AemulusModManager.Utilities;
using AemulusModManager.Utilities.AwbMerging;
using AemulusModManager.Utilities.FileMerging;

namespace AemulusModManager;

/// <summary>
/// Cross-platform port of BinMerger. Handles unpacking mod archives,
/// merging loose files into .bin/.pak/.pac archives via PAKPack,
/// and SPD/SPR merging.
/// </summary>
public static class binMerge
{
    private static string AppDir => AemulusModManager.Avalonia.Utilities.AppPaths.ExeDir;
    private static string DataDir => AemulusModManager.Avalonia.Utilities.AppPaths.DataDir;

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bin", ".abin", ".fpc", ".arc", ".pak", ".pac", ".pack", ".gsd", ".tpc"
    };

    private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aem", ".tblpatch", ".xml", ".png", ".jpg", ".7z", ".bat", ".txt",
        ".zip", ".json", ".tbp", ".rar", ".exe", ".dll", ".flow", ".msg",
        ".back", ".bp", ".pnach"
    };

    // Normalize a Windows-style path (with backslashes) to the current OS separator
    private static string NormalizePath(string path) => path.Replace('\\', Path.DirectorySeparatorChar);

    /// <summary>
    /// Case-insensitive File.Exists for Linux. Tries the given path, then checks for
    /// a case-insensitive match in the parent directory.
    /// </summary>
    private static bool FileExistsCI(string path)
    {
        if (File.Exists(path)) return true;
        if (OperatingSystem.IsWindows()) return false; // Windows FS is already case-insensitive
        var dir = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);
        if (dir == null || !Directory.Exists(dir)) return false;
        return Directory.EnumerateFiles(dir)
            .Any(f => Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves a path to its actual casing on disk. Returns the original path if no match found.
    /// </summary>
    private static string ResolvePathCI(string path)
    {
        if (File.Exists(path)) return path;
        if (OperatingSystem.IsWindows()) return path;
        var dir = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);
        if (dir == null || !Directory.Exists(dir)) return path;
        var match = Directory.EnumerateFiles(dir)
            .FirstOrDefault(f => Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase));
        return match ?? path;
    }

    private static List<string> getModList(string dir)
    {
        var mods = new List<string>();
        var modsAem = Path.Combine(dir, "mods.aem");
        if (File.Exists(modsAem))
        {
            foreach (var line in File.ReadAllLines(modsAem))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    mods.Add(line);
            }
        }
        return mods;
    }

    public static void DeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            foreach (string entry in Directory.EnumerateFileSystemEntries(path))
            {
                if (File.Exists(entry))
                    File.Delete(entry);
                else if (Directory.Exists(entry))
                    Directory.Delete(entry, true);
            }
            Directory.Delete(path);
        }
        catch (Exception ex)
        {
            ParallelLogger.Log("[ERROR] An error occurred: " + ex.Message);
        }
    }

    private static bool IsArchiveExtension(string ext) => ArchiveExtensions.Contains(ext);

    private static void UnpackSPD(string file)
    {
        ParallelLogger.Log($"[INFO] Unpacking {file}...");
        var spdFolder = Path.ChangeExtension(file, null);
        Directory.CreateDirectory(spdFolder);
        var ddsFiles = spdUtils.getDDSFiles(file);
        foreach (var dds in ddsFiles)
            File.WriteAllBytes(Path.Combine(spdFolder, $"{dds.name}.dds"), dds.file);
        var spdKeys = spdUtils.getSPDKeys(file);
        foreach (var key in spdKeys)
            File.WriteAllBytes(Path.Combine(spdFolder, $"{key.id}.spdspr"), key.file);
    }

    private static void UnpackSPR(string file, string game)
    {
        if (game == "Persona Q2") return;
        ParallelLogger.Log($"[INFO] Unpacking {file}...");
        var sprFolder = Path.ChangeExtension(file, null);
        Directory.CreateDirectory(sprFolder);
        var tmxNames = sprUtils.getTmxNames(file);
        foreach (var name in tmxNames.Keys)
        {
            byte[] tmx = sprUtils.extractTmx(file, name);
            File.WriteAllBytes(Path.Combine(sprFolder, $"{name}.tmx"), tmx);
        }
    }

    private static void RecursiveUnpackArchives(string baseDir, string game, int depth = 0)
    {
        if (depth > 3 || !Directory.Exists(baseDir)) return;
        foreach (var f in Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(f).ToLower();
            if (IsArchiveExtension(ext))
            {
                ParallelLogger.Log($"[INFO] Unpacking {f}...");
                PAKPackHelper.PAKPackCMD($"unpack \"{f}\"");
                RecursiveUnpackArchives(Path.ChangeExtension(f, null), game, depth + 1);
            }
            else if (ext == ".spd")
            {
                UnpackSPD(f);
            }
            else if (ext == ".spr")
            {
                UnpackSPR(f, game);
            }
        }
    }

    public static void Unpack(List<string> ModList, string modDir, bool useCpk, string cpkLang, string game)
    {
        var pakPackAvailable = PAKPackHelper.IsAvailable();
        if (!pakPackAvailable)
        {
            ParallelLogger.Log("[WARNING] PAKPack not available - archive unpacking will be skipped, but loose files will still be copied.");
            ParallelLogger.Log("[WARNING] Please check Dependencies/PAKPack/ and ensure mono is installed on Linux.");
        }
        ParallelLogger.Log("[INFO] Beginning to unpack...");

        // Copy over base PATCH1 file for P5R Switch
        if (game == "Persona 5 Royal (Switch)")
        {
            var srcMov = Path.Combine(DataDir, "Original", game, "PATCH1", "MOVIE", "MOV000.USM");
            if (File.Exists(srcMov))
            {
                ParallelLogger.Log("[INFO] Copying over base PATCH1 file");
                var destMov = Path.Combine(modDir, "PATCH1", "MOVIE", "MOV000.USM");
                Directory.CreateDirectory(Path.GetDirectoryName(destMov)!);
                File.Copy(srcMov, destMov, true);
            }
            else
                ParallelLogger.Log($"[WARNING] {srcMov} not found, try unpacking base files again");
        }

        foreach (var mod in ModList)
        {
            if (!Directory.Exists(mod))
            {
                ParallelLogger.Log($"[ERROR] Cannot find {mod}");
                continue;
            }

            // Run prebuild script
            var prebuildBat = Path.Combine(mod, "prebuild.bat");
            var prebuildSh = Path.Combine(mod, "prebuild.sh");
            if (!OperatingSystem.IsWindows() && File.Exists(prebuildSh) && new FileInfo(prebuildSh).Length > 0)
            {
                ParallelLogger.Log($"[INFO] Running {prebuildSh}...");
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/sh",
                        Arguments = $"\"{prebuildSh}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetFullPath(mod)
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    proc?.WaitForExit();
                    ParallelLogger.Log($"[INFO] Finished running {prebuildSh}!");
                }
                catch (Exception ex)
                {
                    ParallelLogger.Log($"[WARNING] Failed to run prebuild.sh: {ex.Message}");
                }
            }
            else if (OperatingSystem.IsWindows() && File.Exists(prebuildBat) && new FileInfo(prebuildBat).Length > 0)
            {
                ParallelLogger.Log($"[INFO] Running {prebuildBat}...");
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.GetFullPath(prebuildBat),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetFullPath(mod)
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit();
                ParallelLogger.Log($"[INFO] Finished running {prebuildBat}!");
            }

            var modList = getModList(mod);
            string[]? aemIgnore = File.Exists(Path.Combine(mod, "Ignore.aem"))
                ? File.ReadAllLines(Path.Combine(mod, "Ignore.aem"))
                : null;

            foreach (var file in Directory.GetFiles(mod, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLower();
                var fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                var relFromMod = Path.GetRelativePath(mod, file);

                // Skip metadata/excluded files
                if (SkipExtensions.Contains(ext)) continue;
                if (relFromMod.Contains("spdpatches", StringComparison.OrdinalIgnoreCase)) continue;
                if (fileName == "preview") continue;
                if (relFromMod.Contains(Path.Combine("texture_override", ""), StringComparison.OrdinalIgnoreCase)) continue;
                if (game == "Persona 3 Portable" && relFromMod.Contains(Path.Combine("FMV", ""), StringComparison.OrdinalIgnoreCase)) continue;
                if ((game == "Persona 3 Portable" || game == "Persona 1 (PSP)") && relFromMod.Contains(Path.Combine("cheats", ""), StringComparison.OrdinalIgnoreCase)) continue;

                var binPath = Path.Combine(modDir, relFromMod);
                var ogBinPath = Path.Combine(DataDir, "Original", game, relFromMod);

                if (aemIgnore != null && aemIgnore.Any(file.Contains)) continue;
                if (AwbMerger.SoundArchiveExists(Path.GetDirectoryName(ogBinPath))) continue;

                if (game != "Persona 1 (PSP)" && IsArchiveExtension(ext))
                {
                    if (FileExistsCI(ogBinPath) && modList.Count > 0)
                    {
                        // Check if mods.aem specifies contents for this archive
                        var archiveName = Path.GetFileNameWithoutExtension(binPath);
                        var archiveDir = Path.GetDirectoryName(relFromMod) ?? "";
                        // Normalize mods.aem entries (which use \ on Windows) for comparison
                        var expectedPrefix = Path.Combine(archiveDir, archiveName) + Path.DirectorySeparatorChar;
                        var expectedPrefixWin = archiveDir.Replace(Path.DirectorySeparatorChar, '\\') + "\\" + archiveName + "\\";

                        if (!modList.Exists(x => NormalizePath(x).StartsWith(NormalizePath(expectedPrefix), StringComparison.OrdinalIgnoreCase)
                            || x.StartsWith(expectedPrefixWin, StringComparison.OrdinalIgnoreCase)))
                        {
                            ParallelLogger.Log($"[WARNING] Using {binPath} as base since nothing was specified in mods.aem");
                            if (useCpk)
                                binPath = Regex.Replace(binPath, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                            Directory.CreateDirectory(Path.GetDirectoryName(binPath)!);
                            File.Copy(file, binPath, true);
                            continue;
                        }

                        ParallelLogger.Log($"[INFO] Unpacking {file}...");
                        if (pakPackAvailable)
                        {
                            PAKPackHelper.PAKPackCMD($"unpack \"{file}\"");
                            RecursiveUnpackArchives(Path.ChangeExtension(file, null), game);
                        }
                        else
                        {
                            ParallelLogger.Log($"[WARNING] Skipping archive unpack for {file} (PAKPack not available)");
                            // Fall back to copying the archive file as-is
                            if (useCpk)
                                binPath = Regex.Replace(binPath, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                            Directory.CreateDirectory(Path.GetDirectoryName(binPath)!);
                            File.Copy(file, binPath, true);
                        }
                    }
                    else
                    {
                        if (useCpk)
                        {
                            binPath = Regex.Replace(binPath, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                            binPath = Regex.Replace(binPath, "movie0000[0-2]", "movie");
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(binPath)!);
                        File.Copy(file, binPath, true);
                        ParallelLogger.Log($"[INFO] Copying over {file} to {binPath}");
                    }
                }
                else if (game != "Persona 1 (PSP)" && ext == ".spd")
                {
                    if (FileExistsCI(ogBinPath) && modList.Count > 0)
                    {
                        UnpackSPD(file);
                    }
                    else
                    {
                        if (useCpk)
                        {
                            binPath = Regex.Replace(binPath, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                            binPath = Regex.Replace(binPath, "movie0000[0-2]", "movie");
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(binPath)!);
                        File.Copy(file, binPath, true);
                        ParallelLogger.Log($"[INFO] Copying over {file} to {binPath}");
                    }
                }
                else if (game != "Persona 1 (PSP)" && ext == ".spr" && game != "Persona Q2")
                {
                    if (FileExistsCI(ogBinPath) && modList.Count > 0)
                    {
                        UnpackSPR(file, game);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(binPath)!);
                        File.Copy(file, binPath, true);
                        ParallelLogger.Log($"[INFO] Copying over {file} to {binPath}");
                    }
                }
                else
                {
                    if (useCpk)
                    {
                        binPath = Regex.Replace(binPath, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                        binPath = Regex.Replace(binPath, "movie0000[0-2]", "movie");
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(binPath)!);
                    File.Copy(file, binPath, true);
                    ParallelLogger.Log($"[INFO] Copying over {file} to {binPath}");
                }
            }

            // Copy over loose files specified by mods.aem
            foreach (var m in modList)
            {
                var srcFile = Path.Combine(mod, NormalizePath(m));
                if (File.Exists(srcFile))
                {
                    var dir = Path.Combine(modDir, NormalizePath(m));
                    if (useCpk)
                    {
                        dir = Regex.Replace(dir, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                        dir = Regex.Replace(dir, "movie0000[0-2]", "movie");
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(dir)!);
                    File.Copy(srcFile, dir, true);
                    ParallelLogger.Log($"[INFO] Copying over {srcFile} as specified by mods.aem");
                }
            }

            if (game != "Persona 1 (PSP)")
            {
                // Clean up unpacked archive folders
                foreach (var file in Directory.GetFiles(mod, "*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if ((IsArchiveExtension(ext) || ext == ".spd" || ext == ".spr")
                        && Directory.Exists(Path.ChangeExtension(file, null))
                        && Path.GetFileName(Path.ChangeExtension(file, null)) != "result"
                        && Path.GetFileName(Path.ChangeExtension(file, null)) != "panel"
                        && Path.GetFileName(Path.ChangeExtension(file, null)) != "crossword")
                    {
                        DeleteDirectory(Path.ChangeExtension(file, null));
                    }
                }

                // Hardcoded cleanup for special folders
                CleanupSpecialFolders(mod);
            }
        }
        ParallelLogger.Log("[INFO] Finished unpacking!");
    }

    private static void CleanupSpecialFolders(string mod)
    {
        // battle/result special handling
        var battleResult = Path.Combine(mod, "battle", "result");
        if (File.Exists(Path.Combine(mod, "battle", "result.pac")) && Directory.Exists(Path.Combine(battleResult, "result")))
        {
            foreach (var f in Directory.GetFiles(Path.Combine(battleResult, "result")))
            {
                var ext = Path.GetExtension(f).ToLower();
                if (ext == ".gfs" || ext == ".gmd") File.Delete(f);
            }
        }
        if (File.Exists(Path.Combine(mod, "battle", "result", "result.spd")) && Directory.Exists(Path.Combine(battleResult, "result")))
        {
            foreach (var f in Directory.GetFiles(Path.Combine(battleResult, "result")))
            {
                var ext = Path.GetExtension(f).ToLower();
                if (ext == ".dds" || ext == ".spdspr") File.Delete(f);
            }
        }

        // field/panel
        if (File.Exists(Path.Combine(mod, "field", "panel.bin")) && Directory.Exists(Path.Combine(mod, "field", "panel", "panel")))
            DeleteDirectory(Path.Combine(mod, "field", "panel", "panel"));

        // Empty dir cleanup
        if (Directory.Exists(Path.Combine(battleResult, "result")) && !Directory.GetFiles(Path.Combine(battleResult, "result"), "*", SearchOption.AllDirectories).Any())
            DeleteDirectory(Path.Combine(battleResult, "result"));
        if (Directory.Exists(battleResult) && !Directory.GetFiles(battleResult, "*", SearchOption.AllDirectories).Any())
            DeleteDirectory(battleResult);
        var fieldPanel = Path.Combine(mod, "field", "panel");
        if (Directory.Exists(fieldPanel) && !Directory.EnumerateFileSystemEntries(fieldPanel).Any())
            DeleteDirectory(fieldPanel);

        // crossword
        var crossword = Path.Combine(mod, "minigame", "crossword");
        if ((File.Exists(Path.Combine(mod, "minigame", "crossword.pak")) || File.Exists(Path.Combine(mod, "minigame", "crossword.spd")))
            && Directory.Exists(crossword))
        {
            foreach (var f in Directory.GetFiles(crossword))
            {
                if (Path.GetExtension(f).ToLower() != ".pak") File.Delete(f);
            }
        }
        if (Directory.Exists(crossword) && !Directory.GetFiles(crossword, "*", SearchOption.AllDirectories).Any())
            DeleteDirectory(crossword);
    }

    public static void Merge(string modDir, string game)
    {
        ParallelLogger.Log("[INFO] Beginning to merge...");

        // Check if loose folder matches vanilla bin file - copy originals as base
        foreach (var d in Directory.GetDirectories(modDir, "*", SearchOption.AllDirectories))
        {
            var relPath = Path.GetRelativePath(modDir, d);
            var ogPath = Path.Combine(DataDir, "Original", game, relPath);

            CopyOriginalIfNeeded(d, ogPath, ".bin", game, "panel");
            CopyOriginalIfNeeded(d, ogPath, ".abin", game);
            CopyOriginalIfNeeded(d, ogPath, ".fpc", game);
            CopyOriginalIfNeeded(d, ogPath, ".gsd", game);
            CopyOriginalIfNeeded(d, ogPath, ".tpc", game);
            CopyOriginalIfNeeded(d, ogPath, ".arc", game);
            CopyOriginalIfNeeded(d, ogPath, ".pack", game);

            // .pac with special result.pac handling
            var pacPath = FindFileIgnoreExtCase(ogPath, ".pac");
            if (pacPath != null && !FileExistsIgnoreExtCase(d, ".pac"))
            {
                if (Path.GetFileNameWithoutExtension(pacPath).Equals("result", StringComparison.OrdinalIgnoreCase))
                {
                    var resultDir = Path.Combine(d, "result");
                    if (!Directory.Exists(resultDir)) continue;
                    if (!Directory.GetFiles(resultDir, "*.GFS", SearchOption.TopDirectoryOnly).Any()
                        && !Directory.GetFiles(resultDir, "*.GMD", SearchOption.TopDirectoryOnly).Any())
                        continue;
                }
                CopyOriginal(d, pacPath);
            }

            // .pak with special crossword.pak handling
            var pakPath = FindFileIgnoreExtCase(ogPath, ".pak");
            if (pakPath != null && !FileExistsIgnoreExtCase(d, ".pak"))
            {
                if (Path.GetFileNameWithoutExtension(pakPath).Equals("crossword", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Directory.GetFiles(d, "*.dds", SearchOption.AllDirectories).Any()
                        && !Directory.GetFiles(d, "*.spdspr", SearchOption.AllDirectories).Any()
                        && !Directory.GetFiles(d, "*.bmd", SearchOption.AllDirectories).Any()
                        && !Directory.GetFiles(d, "*.plg", SearchOption.AllDirectories).Any())
                        continue;
                }
                CopyOriginal(d, pakPath);
            }

            // .spr (not for PQ2)
            if (game != "Persona Q2")
            {
                var sprPath = FindFileIgnoreExtCase(ogPath, ".spr");
                if (sprPath != null && !FileExistsIgnoreExtCase(d, ".spr"))
                    CopyOriginal(d, sprPath);
            }

            // .spd with special handling
            var spdPath = FindFileIgnoreExtCase(ogPath, ".spd");
            if (spdPath != null && !FileExistsIgnoreExtCase(d, ".spd"))
            {
                var spdName = Path.GetFileNameWithoutExtension(spdPath);
                if (spdName.Equals("result", StringComparison.OrdinalIgnoreCase)
                    || spdName.Equals("crossword", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Directory.GetFiles(d, "*.dds", SearchOption.TopDirectoryOnly).Any()
                        && !Directory.GetFiles(d, "*.spdspr", SearchOption.TopDirectoryOnly).Any())
                        continue;
                }
                CopyOriginal(d, spdPath);
            }
        }

        // Merge files into archives
        foreach (var file in Directory.GetFiles(modDir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLower();
            if (IsArchiveExtension(ext))
            {
                var binFolder = Path.ChangeExtension(file, null);
                if (Directory.Exists(binFolder))
                {
                    MergeArchive(file, binFolder, game);
                }
            }
            else if (ext == ".spd")
            {
                var spdFolder = Path.ChangeExtension(file, null);
                if (Directory.Exists(spdFolder))
                {
                    ParallelLogger.Log($"[INFO] Merging {file}...");
                    foreach (var spdFile in Directory.GetFiles(spdFolder, "*", SearchOption.AllDirectories))
                    {
                        if (Path.GetExtension(spdFile).ToLower() == ".dds")
                        {
                            ParallelLogger.Log($"[INFO] Replacing {spdFile} in {file}");
                            spdUtils.replaceDDS(file, spdFile);
                        }
                        else if (Path.GetExtension(spdFile).ToLower() == ".spdspr")
                        {
                            spdUtils.replaceSPDKey(file, spdFile);
                            ParallelLogger.Log($"[INFO] Replacing {spdFile} in {file}");
                        }
                    }
                }
            }
            else if (game != "Persona Q2" && ext == ".spr")
            {
                var sprFolder = Path.ChangeExtension(file, null);
                if (Directory.Exists(sprFolder))
                {
                    ParallelLogger.Log($"[INFO] Merging {file}...");
                    foreach (var sprFile in Directory.GetFiles(sprFolder, "*", SearchOption.AllDirectories))
                    {
                        ParallelLogger.Log($"[INFO] Replacing {sprFile} in {file}");
                        sprUtils.replaceTmx(file, sprFile);
                    }
                }
            }
        }

        // Clean up unpacked folders
        foreach (var file in Directory.GetFiles(modDir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLower();
            if ((IsArchiveExtension(ext) || ext == ".spd" || ext == ".spr")
                && Directory.Exists(Path.ChangeExtension(file, null))
                && Path.GetFileName(Path.ChangeExtension(file, null)) != "result"
                && Path.GetFileName(Path.ChangeExtension(file, null)) != "panel"
                && Path.GetFileName(Path.ChangeExtension(file, null)) != "crossword")
            {
                DeleteDirectory(Path.ChangeExtension(file, null));
            }
        }

        // Hardcoded cleanup cases
        var battleResult = Path.Combine(modDir, "battle");
        if (File.Exists(Path.Combine(battleResult, "result.pac")) && !File.Exists(Path.Combine(battleResult, "result", "result.spd"))
            && Directory.Exists(Path.Combine(battleResult, "result")))
            DeleteDirectory(Path.Combine(battleResult, "result"));
        if (Directory.Exists(Path.Combine(battleResult, "result", "result")))
            DeleteDirectory(Path.Combine(battleResult, "result", "result"));
        var crosswordDir = Path.Combine(modDir, "minigame", "crossword");
        if (Directory.Exists(Path.Combine(crosswordDir, "crossword")))
            DeleteDirectory(Path.Combine(crosswordDir, "crossword"));
        var panelDir = Path.Combine(modDir, "field", "panel");
        if (Directory.Exists(Path.Combine(panelDir, "panel")))
            DeleteDirectory(Path.Combine(panelDir, "panel"));
        if (Directory.Exists(panelDir) && !Directory.EnumerateFileSystemEntries(panelDir).Any())
            DeleteDirectory(panelDir);
        if (Directory.Exists(Path.Combine(crosswordDir, "crossword")))
            DeleteDirectory(Path.Combine(crosswordDir, "crossword"));
        if (Directory.Exists(crosswordDir))
        {
            foreach (var file in Directory.GetFiles(crosswordDir, "*", SearchOption.AllDirectories))
                if (Path.GetExtension(file).ToLower() != ".pak")
                    File.Delete(file);
        }
        if (Directory.Exists(crosswordDir) && !Directory.EnumerateFileSystemEntries(crosswordDir).Any())
            DeleteDirectory(crosswordDir);

        ParallelLogger.Log("[INFO] Finished merging!");
    }

    /// <summary>
    /// Find a file by trying multiple extension casings (for case-sensitive Linux filesystems).
    /// Returns the actual path found, or null.
    /// </summary>
    private static string? FindFileIgnoreExtCase(string basePath, string ext)
    {
        var path = Path.ChangeExtension(basePath, ext);
        if (File.Exists(path)) return path;
        path = Path.ChangeExtension(basePath, ext.ToUpper());
        if (File.Exists(path)) return path;
        path = Path.ChangeExtension(basePath, ext.ToLower());
        if (File.Exists(path)) return path;
        // Fallback: search directory for case-insensitive match
        var dir = Path.GetDirectoryName(basePath);
        var stem = Path.GetFileNameWithoutExtension(basePath);
        if (dir != null && Directory.Exists(dir))
        {
            var match = Directory.EnumerateFiles(dir)
                .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(stem, StringComparison.OrdinalIgnoreCase)
                    && Path.GetExtension(f).Equals(ext, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return null;
    }

    /// <summary>
    /// Check if a file with the given extension exists at the target path (case-insensitive ext).
    /// </summary>
    private static bool FileExistsIgnoreExtCase(string basePath, string ext)
    {
        return FindFileIgnoreExtCase(basePath, ext) != null;
    }

    private static void CopyOriginalIfNeeded(string d, string ogPath, string ext, string game, string? skipDirName = null)
    {
        var fullOgPath = FindFileIgnoreExtCase(ogPath, ext);
        if (fullOgPath != null && !FileExistsIgnoreExtCase(d, ext))
        {
            if (skipDirName != null && Path.GetFileNameWithoutExtension(fullOgPath)
                    .Equals(skipDirName, StringComparison.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(Path.Combine(d, skipDirName)))
                    return;
            }
            CopyOriginal(d, fullOgPath);
        }
    }

    private static void CopyOriginal(string d, string ogPath)
    {
        ParallelLogger.Log($"[INFO] Copying over {ogPath} to use as base.");
        Directory.CreateDirectory(Path.GetDirectoryName(d)!);
        File.Copy(ogPath, Path.Combine(Path.GetDirectoryName(d)!, Path.GetFileName(ogPath)), false);
    }

    private static int commonPrefixUtil(string str1, string str2)
    {
        int n1 = str1.Length, n2 = str2.Length;
        int result = 0;
        for (int i = 0; i < Math.Min(n1, n2); i++)
        {
            if (!str1[i].ToString().Equals(str2[i].ToString(), StringComparison.OrdinalIgnoreCase))
                break;
            result++;
        }
        return result;
    }

    /// <summary>
    /// Case-insensitive lookup in the archive's content list.
    /// Returns the actual archive-internal path, or null if not found.
    /// </summary>
    private static string? ContentsFind(List<string> contents, string path)
    {
        return contents.FirstOrDefault(c => c.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    private static void MergeArchive(string archivePath, string binFolder, string game)
    {
        ParallelLogger.Log($"[INFO] Merging {archivePath}...");
        var bin = archivePath;

        // PAKPack uses forward-slash paths internally
        var contents = PAKPackHelper.GetFileContents(bin);
        ParallelLogger.Log($"[INFO] Archive {Path.GetFileName(bin)} has {contents.Count} entries");

        // Unpack archive to temp for nested operations
        var temp = $"{binFolder}_temp";
        PAKPackHelper.PAKPackCMD($"unpack \"{bin}\" \"{temp}\"");

        foreach (var f in Directory.GetFiles(binFolder, "*", SearchOption.AllDirectories))
        {
            // Get relative path from the archive folder, using forward slashes for PAKPack
            var relPath = Path.GetRelativePath(binFolder, f).Replace(Path.DirectorySeparatorChar, '/');

            // Try various prefix patterns used in Persona archives (case-insensitive)
            string? match;
            if ((match = ContentsFind(contents, $"../../../{relPath}")) != null)
            {
                PAKPackHelper.PAKPackCMD($"replace \"{bin}\" {match} \"{f}\" \"{bin}\"");
            }
            else if ((match = ContentsFind(contents, $"../../{relPath}")) != null)
            {
                PAKPackHelper.PAKPackCMD($"replace \"{bin}\" {match} \"{f}\" \"{bin}\"");
            }
            else if ((match = ContentsFind(contents, $"../{relPath}")) != null)
            {
                PAKPackHelper.PAKPackCMD($"replace \"{bin}\" {match} \"{f}\" \"{bin}\"");
            }
            else if ((match = ContentsFind(contents, relPath)) != null)
            {
                PAKPackHelper.PAKPackCMD($"replace \"{bin}\" {match} \"{f}\" \"{bin}\"");
            }
            else
            {
                // Need nested unpacking - find best matching content entry
                var longestPrefix = FindBestMatch(contents, relPath);

                if (string.IsNullOrEmpty(longestPrefix))
                {
                    ParallelLogger.Log($"[WARNING] Could not find matching content for {relPath} in {bin}, skipping");
                    continue;
                }

                var lpExt = Path.GetExtension(longestPrefix).ToLower();
                if (IsArchiveExtension(lpExt))
                {
                    HandleNestedArchive(bin, temp, f, relPath, longestPrefix, contents, game);
                }
                else if (lpExt == ".spd" && (Path.GetExtension(f).ToLower() == ".dds" || Path.GetExtension(f).ToLower() == ".spdspr"))
                {
                    var spdPath = Path.Combine(temp, longestPrefix.Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (File.Exists(spdPath.Replace("_temp", "")))
                        File.Copy(spdPath.Replace("_temp", ""), spdPath, true);
                    if (Path.GetExtension(f).ToLower() == ".dds")
                        spdUtils.replaceDDS(spdPath, f);
                    else
                        spdUtils.replaceSPDKey(spdPath, f);
                    ParallelLogger.Log($"[INFO] Replacing {spdPath} in {f}");
                    PAKPackHelper.PAKPackCMD($"replace \"{bin}\" {longestPrefix} \"{spdPath}\" \"{bin}\"");
                }
                else if (lpExt == ".spr" && Path.GetExtension(f).ToLower() == ".tmx")
                {
                    var path = longestPrefix.Replace("../", "");
                    var sprPath = Path.Combine(temp, path.Replace("/", Path.DirectorySeparatorChar.ToString()));
                    sprUtils.replaceTmx(sprPath, f);
                    PAKPackHelper.PAKPackCMD($"replace \"{bin}\" {longestPrefix} \"{sprPath}\" \"{bin}\"");
                }
            }
        }
        DeleteDirectory(temp);
    }

    private static string FindBestMatch(List<string> contents, string relPath)
    {
        string longestPrefix = "";
        int longestPrefixLen = 0;

        foreach (var c in contents)
        {
            int prefixLen = commonPrefixUtil(c, relPath);
            int otherPrefixLen = commonPrefixUtil(c, $"../../{relPath}");
            int otherOtherPrefixLen = commonPrefixUtil(c, $"../{relPath}");
            int maxLen = Math.Max(Math.Max(prefixLen, otherPrefixLen), otherOtherPrefixLen);

            if (maxLen > longestPrefixLen)
            {
                longestPrefix = c;
                longestPrefixLen = maxLen;
            }
            else if (maxLen == longestPrefixLen)
            {
                // Prefer archive/container extensions
                if (IsContainerExtension(Path.GetExtension(c)) && !IsContainerExtension(Path.GetExtension(longestPrefix)))
                    longestPrefix = c;
                else if (maxLen > 0 && c[longestPrefixLen] == '.')
                    longestPrefix = c;
            }
        }
        return longestPrefix;
    }

    private static bool IsContainerExtension(string ext)
    {
        return IsArchiveExtension(ext) || ext.Equals(".spd", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".spr", StringComparison.OrdinalIgnoreCase);
    }

    private static void HandleNestedArchive(string bin, string temp, string f, string relPath, string longestPrefix,
        List<string> contents, string game)
    {
        var file2 = Path.Combine(temp, longestPrefix.Replace("/", Path.DirectorySeparatorChar.ToString()));
        var contents2 = PAKPackHelper.GetFileContents(file2);

        var split = relPath.Split('/');
        var numPrefixFolders = longestPrefix.Split('/').Length;
        var binPath2 = string.Join("/", split.Skip(numPrefixFolders));

        var match2 = ContentsFind(contents2, binPath2);
        if (match2 != null)
        {
            PAKPackHelper.PAKPackCMD($"replace \"{file2}\" {match2} \"{f}\" \"{file2}\"");
            PAKPackHelper.PAKPackCMD($"replace \"{bin}\" {longestPrefix} \"{file2}\" \"{bin}\"");
        }
        else
        {
            // Try one more level of nesting
            var longestPrefix2 = FindBestMatch(contents2, binPath2);
            var lp2Ext = Path.GetExtension(longestPrefix2).ToLower();

            if (IsArchiveExtension(lp2Ext))
            {
                var lpNoExt = Path.ChangeExtension(longestPrefix, null).Replace("/", Path.DirectorySeparatorChar.ToString());
                var lp2Normalized = longestPrefix2.Replace("/", Path.DirectorySeparatorChar.ToString());
                var file3 = Path.Combine(temp, lpNoExt, lp2Normalized);
                PAKPackHelper.PAKPackCMD($"unpack \"{file2}\"");
                var contents3 = PAKPackHelper.GetFileContents(file3);

                var split2 = binPath2.Split('/');
                var numPrefixFolders2 = longestPrefix2.Split('/').Length;
                var binPath3 = string.Join("/", split2.Skip(numPrefixFolders2));

                var match3 = ContentsFind(contents3, binPath3);
                if (match3 != null)
                {
                    PAKPackHelper.PAKPackCMD($"replace \"{file3}\" {match3} \"{f}\" \"{file3}\"");
                    PAKPackHelper.PAKPackCMD($"replace \"{file2}\" {longestPrefix2} \"{file3}\" \"{file2}\"");
                    PAKPackHelper.PAKPackCMD($"replace \"{bin}\" {longestPrefix} \"{file2}\" \"{bin}\"");
                }
            }
            else if (lp2Ext == ".spd" && (Path.GetExtension(f).ToLower() == ".dds" || Path.GetExtension(f).ToLower() == ".spdspr"))
            {
                PAKPackHelper.PAKPackCMD($"unpack \"{file2}\"");
                var lpNoExt = Path.ChangeExtension(longestPrefix, null).Replace("/", Path.DirectorySeparatorChar.ToString());
                var lp2Normalized = longestPrefix2.Replace("/", Path.DirectorySeparatorChar.ToString());
                var spdPath = Path.Combine(temp, lpNoExt, lp2Normalized);
                if (Path.GetExtension(f).ToLower() == ".dds")
                    spdUtils.replaceDDS(spdPath, f);
                else
                    spdUtils.replaceSPDKey(spdPath, f);
                ParallelLogger.Log($"[INFO] Replacing {spdPath} in {f}");
                PAKPackHelper.PAKPackCMD($"replace \"{file2}\" {longestPrefix2} \"{spdPath}\" \"{file2}\"");
                PAKPackHelper.PAKPackCMD($"replace \"{bin}\" {longestPrefix} \"{file2}\" \"{bin}\"");
            }
            else if (game != "Persona Q2" && lp2Ext == ".spr" && Path.GetExtension(f).ToLower() == ".tmx")
            {
                PAKPackHelper.PAKPackCMD($"unpack \"{file2}\"");
                var lpNoExt = Path.ChangeExtension(longestPrefix, null).Replace("/", Path.DirectorySeparatorChar.ToString());
                var lp2Normalized = longestPrefix2.Replace("/", Path.DirectorySeparatorChar.ToString());
                var sprPath = Path.Combine(temp, lpNoExt, lp2Normalized);
                sprUtils.replaceTmx(sprPath, f);
                PAKPackHelper.PAKPackCMD($"replace \"{file2}\" {longestPrefix2} \"{sprPath}\" \"{file2}\"");
                PAKPackHelper.PAKPackCMD($"replace \"{bin}\" {longestPrefix} \"{file2}\" \"{bin}\"");
            }
        }
    }

    public static void Restart(string modDir, bool emptySND, string game, string cpkLang, string? cheats, string? cheatsWS, bool empty = false)
    {
        ParallelLogger.Log("[INFO] Deleting current mod build...");

        // Revert appended cpks for P4G
        if (game == "Persona 4 Golden")
        {
            var path = Path.GetDirectoryName(modDir);
            if (path != null)
            {
                var origCpk = Path.Combine(DataDir, "Original", "Persona 4 Golden", cpkLang);
                var gameCpk = Path.Combine(path, cpkLang);
                if (File.Exists(origCpk) && File.Exists(gameCpk) && GetChecksumString(origCpk) != GetChecksumString(gameCpk))
                {
                    ParallelLogger.Log($"[INFO] Reverting {cpkLang} back to original");
                    File.Copy(origCpk, gameCpk, true);
                }
                var origMovie = Path.Combine(DataDir, "Original", "Persona 4 Golden", "movie.cpk");
                var gameMovie = Path.Combine(path, "movie.cpk");
                if (File.Exists(origMovie) && File.Exists(gameMovie) && GetChecksumString(origMovie) != GetChecksumString(gameMovie))
                {
                    ParallelLogger.Log("[INFO] Reverting movie.cpk back to original");
                    File.Copy(origMovie, gameMovie, true);
                }
                var data7 = Path.Combine(path, "data00007.pac");
                if (File.Exists(data7)) { ParallelLogger.Log("[INFO] Deleting data00007.pac"); File.Delete(data7); }
                var movie3 = Path.Combine(path, "movie00003.pac");
                if (File.Exists(movie3)) { ParallelLogger.Log("[INFO] Deleting movie00003.pac"); File.Delete(movie3); }
            }
        }

        if (!emptySND || game == "Persona 3 FES")
        {
            ParallelLogger.Log("[INFO] Keeping SND folder.");
            if (Directory.Exists(modDir))
            {
                foreach (var dir in Directory.GetDirectories(modDir))
                {
                    if (!Path.GetFileName(dir).Equals("snd", StringComparison.OrdinalIgnoreCase))
                        DeleteDirectory(dir);
                }
                foreach (var file in Directory.GetFiles(modDir))
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if (ext != ".elf" && ext != ".iso")
                        File.Delete(file);
                }
            }
        }
        else
        {
            if (Directory.Exists(modDir))
                DeleteDirectory(modDir);
            Directory.CreateDirectory(modDir);
        }

        if ((game == "Persona Q2" || game == "Persona Q") && empty)
        {
            File.Create(Path.Combine(modDir, "dummy.txt")).Dispose();
            MakeCpk(modDir, true, empty);
        }

        // Delete Aemulus pnaches
        if (game == "Persona 3 FES" && cheats != null && Directory.Exists(cheats))
        {
            var aemPnaches = Directory.GetFiles(cheats, "*_aem.pnach", SearchOption.TopDirectoryOnly);
            ParallelLogger.Log($"[INFO] Cleaning {aemPnaches.Length} _aem.pnach file(s) from {cheats}");
            foreach (var pnach in aemPnaches)
            {
                File.Delete(pnach);
                ParallelLogger.Log($"[INFO] Deleted {Path.GetFileName(pnach)}");
            }
        }
        else if (game == "Persona 3 FES")
        {
            ParallelLogger.Log($"[WARNING] Cheats path not set or doesn't exist (cheats={cheats}), skipping _aem.pnach cleanup");
        }
        if (game == "Persona 3 FES" && cheatsWS != null && Directory.Exists(cheatsWS))
        {
            var aemPnaches = Directory.GetFiles(cheatsWS, "*_aem.pnach", SearchOption.TopDirectoryOnly);
            ParallelLogger.Log($"[INFO] Cleaning {aemPnaches.Length} _aem.pnach file(s) from {cheatsWS}");
            foreach (var pnach in aemPnaches)
            {
                File.Delete(pnach);
                ParallelLogger.Log($"[INFO] Deleted {Path.GetFileName(pnach)}");
            }
        }
    }

    public static string GetChecksumString(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "");
    }

    public static void MakeCpk(string modDir, bool crc, bool empty = false)
    {
        var cpkMake = Path.Combine(AppDir, "Dependencies", "CpkMakeC", "cpkmakec.exe");
        if (!File.Exists(cpkMake))
        {
            // Try system-installed alternative on Linux
            ParallelLogger.Log($"[WARNING] cpkmakec.exe not found at {cpkMake}. CPK creation skipped - this is a Windows-only tool.");
            ParallelLogger.Log("[INFO] If your game requires CPK output, copy the built files manually or use a CPK tool.");
            return;
        }

        var extension = Path.GetFileName(modDir) == "PATCH1" ? "CPK" : "cpk";
        var args = $"\"{modDir}\" \"{modDir}\".{extension} -mode=FILENAME";
        if (crc) args += " -crc";
        if (!empty) ParallelLogger.Log($"[INFO] Building {Path.GetFileName(modDir)}.{extension}...");

        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = cpkMake,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
        };
        process.Start();
        process.WaitForExit();
    }

    public static void LoadCheats(List<string> mods, string cheatsDir)
    {
        foreach (var dir in mods)
        {
            ParallelLogger.Log($"[INFO] Searching for cheats in {dir}...");
            var cheatsFolder = Path.Combine(dir, "cheats");
            if (!Directory.Exists(cheatsFolder))
            {
                ParallelLogger.Log($"[INFO] No cheats folder found in {dir}");
                continue;
            }
            foreach (var cheat in Directory.GetFiles(cheatsFolder, "*.pnach", SearchOption.AllDirectories))
            {
                File.Copy(cheat, Path.Combine(cheatsDir, $"{Path.GetFileNameWithoutExtension(cheat)}_aem.pnach"), true);
                ParallelLogger.Log($"[INFO] Copied over {Path.GetFileNameWithoutExtension(cheat)}_aem.pnach to {cheatsDir}");
            }
        }
    }

    public static void LoadCheatsWS(List<string> mods, string cheatsDir)
    {
        foreach (var dir in mods)
        {
            ParallelLogger.Log($"[INFO] Searching for cheats_ws in {dir}...");
            var cheatsFolder = Path.Combine(dir, "cheats_ws");
            if (!Directory.Exists(cheatsFolder))
            {
                ParallelLogger.Log($"[INFO] No cheats_ws folder found in {dir}");
                continue;
            }
            foreach (var cheat in Directory.GetFiles(cheatsFolder, "*.pnach", SearchOption.AllDirectories))
            {
                File.Copy(cheat, Path.Combine(cheatsDir, $"{Path.GetFileNameWithoutExtension(cheat)}_aem.pnach"), true);
                ParallelLogger.Log($"[INFO] Copied over {Path.GetFileNameWithoutExtension(cheat)}_aem.pnach to {cheatsDir}");
            }
        }
    }

    public static void LoadTextures(List<string> mods, string texturesDir)
    {
        foreach (var dir in mods)
        {
            ParallelLogger.Log($"[INFO] Searching for textures in {dir}...");
            var texFolder = Path.Combine(dir, "texture_override");
            if (!Directory.Exists(texFolder))
            {
                ParallelLogger.Log($"[INFO] No textures folder found in {dir}");
                continue;
            }
            foreach (var texture in Directory.GetFiles(texFolder, "*", SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(texFolder, texture);
                var destPath = Path.Combine(texturesDir, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(texture, destPath, true);
                ParallelLogger.Log($"[INFO] Copied over {Path.GetFileName(texture)} to {destPath}");
            }
        }
    }

    public static void LoadFMVs(List<string> mods, string modDir)
    {
        var copiedFmvs = new List<string>();
        foreach (var dir in mods)
        {
            var fmvDir = Path.Combine(dir, "FMV");
            if (!Directory.Exists(fmvDir)) continue;
            var destFmvDir = Path.Combine(modDir, "FMV");
            Directory.CreateDirectory(destFmvDir);

            foreach (var fmv in Directory.GetFiles(fmvDir, "*.pmsf"))
            {
                copiedFmvs.Add(Path.GetFileName(fmv));
                var destFmv = Path.Combine(destFmvDir, Path.GetFileName(fmv));
                try
                {
                    File.Copy(fmv, destFmv, true);
                    ParallelLogger.Log($"[INFO] Copying {fmv} over {destFmv}");
                }
                catch (Exception ex)
                {
                    ParallelLogger.Log($"[ERROR] Unable to copy {fmv} to {destFmv}: {ex.Message}");
                }
            }
        }
        var fmvOutputDir = Path.Combine(modDir, "FMV");
        if (Directory.Exists(fmvOutputDir))
        {
            foreach (var file in Directory.EnumerateFiles(fmvOutputDir).Where(f => !copiedFmvs.Contains(Path.GetFileName(f))))
            {
                try
                {
                    File.Delete(file);
                    ParallelLogger.Log($"[INFO] Deleting unwanted FMV {file}");
                }
                catch (Exception ex)
                {
                    ParallelLogger.Log($"[ERROR] Unable to delete unwanted FMV {file}: {ex.Message}");
                }
            }
        }
    }

    public static void LoadP3PCheats(List<string> mods, string cheatFile)
    {
        foreach (var dir in mods)
        {
            ParallelLogger.Log($"[INFO] Searching for cheats in {dir}...");
            var cheatsFolder = Path.Combine(dir, "cheats");
            if (!Directory.Exists(cheatsFolder))
            {
                ParallelLogger.Log($"[INFO] No cheats folder found in {dir}");
                continue;
            }

            var existingCheats = PPSSPPCheatFile.ParseCheats(cheatFile);
            foreach (var newCheatFile in Directory.GetFiles(cheatsFolder, "*.ini"))
            {
                ParallelLogger.Log($"[INFO] Applying cheats from {newCheatFile}");
                var newCheats = PPSSPPCheatFile.ParseCheats(newCheatFile);
                foreach (var cheat in newCheats.Cheats)
                {
                    var existing = existingCheats.Cheats.FirstOrDefault(c => c.Name == cheat.Name);
                    if (existing != null)
                        existing.Contents = cheat.Contents;
                    else
                        existingCheats.Cheats.Add(cheat);
                }
            }

            using var writer = new StreamWriter(cheatFile);
            writer.WriteLine($"_S {existingCheats.GameID}");
            writer.WriteLine($"_G {existingCheats.GameName}");
            writer.WriteLine();
            foreach (var cheat in existingCheats.Cheats)
            {
                writer.WriteLine($"_C{(cheat.Enabled ? '1' : '0')} {cheat.Name}");
                foreach (var line in cheat.Contents)
                    writer.WriteLine(line);
            }
        }
    }

    public static void LoadP1PSPCheats(List<string> mods, string cheatFile)
    {
        foreach (var dir in mods)
        {
            ParallelLogger.Log($"[INFO] Searching for cheats in {dir}...");
            if (!Directory.Exists(dir))
            {
                ParallelLogger.Log($"[INFO] No cheats folder found in {dir}");
                continue;
            }

            var cheatsFolder = Path.Combine(dir, "cheats");
            if (!Directory.Exists(cheatsFolder)) continue;

            var existingCheats = PPSSPPCheatFile.ParseCheats(cheatFile);
            foreach (var newCheatFile in Directory.GetFiles(cheatsFolder, "*.ini"))
            {
                ParallelLogger.Log($"[INFO] Applying cheats from {newCheatFile}");
                var newCheats = PPSSPPCheatFile.ParseCheats(newCheatFile);
                foreach (var cheat in newCheats.Cheats)
                {
                    var existing = existingCheats.Cheats.FirstOrDefault(c => c.Name == cheat.Name);
                    if (existing != null)
                        existing.Contents = cheat.Contents;
                    else
                        existingCheats.Cheats.Add(cheat);
                }
            }

            using var writer = new StreamWriter(cheatFile);
            writer.WriteLine($"_S {existingCheats.GameID}");
            writer.WriteLine($"_G {existingCheats.GameName}");
            writer.WriteLine();
            foreach (var cheat in existingCheats.Cheats)
            {
                writer.WriteLine($"_C{(cheat.Enabled ? '1' : '0')} {cheat.Name}");
                foreach (var line in cheat.Contents)
                    writer.WriteLine(line);
            }
        }
    }
}
