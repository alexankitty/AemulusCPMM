using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AemulusModManager.Utilities;
using CriFsV2Lib;
using CriFsV2Lib.Definitions;
using CriFsV2Lib.Definitions.Structs;
using CriFsV2Lib.Definitions.Interfaces;
using CriFsV2Lib.Definitions.Utilities;
using CriFsV2Lib.Encryption.Game;

namespace AemulusModManager;

public static class PacUnpacker {
    internal class FileToExtract : IBatchFileExtractorItem {
        public string FullPath { get; set; }
        public CpkFile File { get; set; }
        public FileToExtract(string fullPath, CpkFile file) {
            FullPath = fullPath;
            File = file;
        }
    }

    private static string AppDir => AemulusModManager.Avalonia.Utilities.AppPaths.ExeDir;
    private static string DataDir => AemulusModManager.Avalonia.Utilities.AppPaths.DataDir;

    private static string? FindSevenZip() {
        // Check bundled first
        var bundled = Path.Combine(AppDir, "Dependencies", "7z", "7z.exe");
        if (File.Exists(bundled)) return bundled;
        var bundled2 = Path.Combine(AppDir, "Dependencies", "7z", "7z");
        if (File.Exists(bundled2)) return bundled2;

        // Check system path (Linux)
        foreach (var name in new[] { "7z", "7za", "7zz" }) {
            try {
                var proc = Process.Start(new ProcessStartInfo {
                    FileName = "which",
                    Arguments = name,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc != null) {
                    var path = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit();
                    if (proc.ExitCode == 0 && !string.IsNullOrEmpty(path))
                        return path;
                }
            }
            catch { }
        }
        return null;
    }

    // P1PSP
    public static async Task UnzipAndUnBin(string iso) {
        var outputDir = Path.Combine(DataDir, "Original", "Persona 1 (PSP)");
        Directory.CreateDirectory(outputDir);
        if (!File.Exists(iso)) {
            ParallelLogger.Log($"[ERROR] Couldn't find {iso}. Please correct the file path in config.");
            return;
        }

        var sevenZip = FindSevenZip();
        if (sevenZip == null) {
            ParallelLogger.Log("[ERROR] Couldn't find 7z. Please install p7zip or 7zip.");
            return;
        }

        ParallelLogger.Log($"[INFO] Extracting files from {iso}");
        await RunProcess(sevenZip, $"x -y \"{iso}\" -o\"{outputDir}\"");

        var ebootPath = Path.Combine(outputDir, "PSP_GAME", "SYSDIR", "EBOOT.BIN");
        var ebootEncPath = Path.Combine(outputDir, "PSP_GAME", "SYSDIR", "EBOOT_ENC.BIN");

        if (File.Exists(ebootPath)) {
            File.Move(ebootPath, ebootEncPath, true);

            // DecEboot is Windows-only, check if available
            var decEboot = Path.Combine(AppDir, "Dependencies", "DecEboot", "deceboot.exe");
            if (File.Exists(decEboot)) {
                ParallelLogger.Log("[INFO] Decrypting EBOOT.BIN");
                await RunProcess(decEboot, $"\"{ebootEncPath}\" \"{ebootPath}\"");
                if (File.Exists(ebootEncPath))
                    File.Delete(ebootEncPath);
            }
            else {
                ParallelLogger.Log("[WARNING] DecEboot not found - EBOOT.BIN not decrypted. Copy decrypted EBOOT.BIN manually.");
                File.Move(ebootEncPath, ebootPath, true);
            }
        }

        ParallelLogger.Log("[INFO] Finished unpacking base files!");
    }

    // P3F
    public static async Task Unzip(string iso) {
        if (!File.Exists(iso)) {
            ParallelLogger.Log($"[ERROR] Couldn't find {iso}. Please correct the file path in config.");
            return;
        }

        var sevenZip = FindSevenZip();
        if (sevenZip == null) {
            ParallelLogger.Log("[ERROR] Couldn't find 7z. Please install p7zip or 7zip.");
            return;
        }

        var outputDir = Path.Combine(DataDir, "Original", "Persona 3 FES");
        Directory.CreateDirectory(outputDir);

        ParallelLogger.Log($"[INFO] Extracting BTL.CVM and DATA.CVM from {iso}");
        await RunProcess(sevenZip, $"x -y \"{iso}\" -o\"{outputDir}\" BTL.CVM DATA.CVM");

        var btlCvm = Path.Combine(outputDir, "BTL.CVM");
        var dataCvm = Path.Combine(outputDir, "DATA.CVM");

        ParallelLogger.Log("[INFO] Extracting base files from BTL.CVM");
        await RunProcess(sevenZip,
            $"x -y \"{btlCvm}\" -o\"{Path.Combine(outputDir, "BTL")}\" *.BIN *.PAK *.PAC *.TBL *.SPR *.BF *.BMD *.PM1 *.bf *.bmd *.pm1 *.FPC -r");

        ParallelLogger.Log("[INFO] Extracting base files from DATA.CVM");
        await RunProcess(sevenZip,
            $"x -y \"{dataCvm}\" -o\"{Path.Combine(outputDir, "DATA")}\" *.BIN *.PAK *.PAC *.TBL *.SPR *.BF *.BMD *.PM1 *.bf *.bmd *.pm1 *.FPC -r");

        ExtractWantedFiles(outputDir);

        if (File.Exists(btlCvm)) File.Delete(btlCvm);
        if (File.Exists(dataCvm)) File.Delete(dataCvm);

        ParallelLogger.Log("[INFO] Finished unpacking base files!");
    }

    // P3P
    public static async Task UnzipAndUnpackCPK(string iso) {
        var outputDir = Path.Combine(DataDir, "Original", "Persona 3 Portable");
        Directory.CreateDirectory(outputDir);
        if (!File.Exists(iso)) {
            ParallelLogger.Log($"[ERROR] Couldn't find {iso}. Please correct the file path in config.");
            return;
        }

        var sevenZip = FindSevenZip();
        if (sevenZip == null) {
            ParallelLogger.Log("[ERROR] Couldn't find 7z. Please install p7zip or 7zip.");
            return;
        }

        ParallelLogger.Log($"[INFO] Extracting umd0.cpk from {iso}");
        await RunProcess(sevenZip, $"x -y \"{iso}\" -o\"{outputDir}\" PSP_GAME/USRDIR/umd0.cpk");

        var csvPath = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", "filtered_umd0.csv");
        var umd0Path = Path.Combine(outputDir, "PSP_GAME", "USRDIR", "umd0.cpk");

        if (File.Exists(csvPath) && File.Exists(umd0Path)) {
            var fileList = File.ReadAllLines(csvPath);
            ParallelLogger.Log("[INFO] Extracting files from umd0.cpk");
            CriFsUnpack(umd0Path, outputDir, fileList);
        }
        else {
            ParallelLogger.Log("[ERROR] Couldn't find umd0.cpk or filter CSV.");
        }

        ExtractWantedFiles(Path.Combine(outputDir, "data"));

        // Cleanup extracted ISO structure
        var pspGameDir = Path.Combine(outputDir, "PSP_GAME");
        if (Directory.Exists(pspGameDir))
            Directory.Delete(pspGameDir, true);

        ParallelLogger.Log("[INFO] Finished unpacking base files!");
    }

    // P4G
    public static void Unpack(string directory, string cpk) {
        var outputDir = Path.Combine(DataDir, "Original", "Persona 4 Golden");
        Directory.CreateDirectory(outputDir);
        if (!Directory.Exists(directory)) {
            ParallelLogger.Log($"[ERROR] Couldn't find {directory}. Please correct the file path in config.");
            return;
        }

        List<string> pacs = new();
        switch (cpk) {
            case "data_e.cpk":
                pacs.Add("data00004.pac");
                pacs.Add("data_e.cpk");
                break;
            case "data.cpk":
                pacs.Add("data00000.pac");
                pacs.Add("data00001.pac");
                pacs.Add("data00003.pac");
                pacs.Add("data.cpk");
                break;
            case "data_k.cpk":
                pacs.Add("data00005.pac");
                pacs.Add("data_k.cpk");
                break;
            case "data_c.cpk":
                pacs.Add("data00006.pac");
                pacs.Add("data_c.cpk");
                break;
        }

        var preappfile = OperatingSystem.IsWindows() ? Path.Combine(AppDir, "Dependencies", "Preappfile", "preappfile.exe") : Path.Combine(AppDir, "Dependencies", "Preappfile", "preappfile");
        if (!File.Exists(preappfile)) {
            ParallelLogger.Log($"[ERROR] Couldn't find preappfile. This tool is Windows-only.");
            ParallelLogger.Log("[INFO] For P4G on Linux, consider extracting the CPK manually with a CRI tool.");
            return;
        }

        List<string> globs = new() { "*[!0-9].bin", "*2[0-1][0-9].bin", "*.arc", "*.pac", "*.pack", "*.bf", "*.bmd", "*.pm1" };
        foreach (var pac in pacs) {
            ParallelLogger.Log($"[INFO] Unpacking files for {pac}...");
            foreach (var glob in globs) {
                var args = $"-i \"{Path.Combine(directory, pac)}\" -o \"{Path.Combine(outputDir, Path.GetFileNameWithoutExtension(pac))}\" --unpack-filter {glob}";
                var proc = Process.Start(new ProcessStartInfo {
                    FileName = preappfile,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                });
                if (proc != null) {
                    while (!proc.HasExited) {
                        var text = proc.StandardOutput.ReadLine();
                        if (!string.IsNullOrEmpty(text))
                            ParallelLogger.Log($"[INFO] {text}");
                    }
                    proc.WaitForExit();
                }
            }
        }

        foreach (var pac in pacs) {
            var pacDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(pac));
            ExtractWantedFiles(pacDir);
        }

        // Backup CPK
        foreach (var cpkFile in new[] { cpk, "movie.cpk" }) {
            var srcPath = Path.Combine(directory, cpkFile);
            var dstPath = Path.Combine(outputDir, cpkFile);
            if (File.Exists(srcPath) && !File.Exists(dstPath)) {
                ParallelLogger.Log($"[INFO] Backing up {cpkFile}");
                File.Copy(srcPath, dstPath, true);
            }
        }

        ParallelLogger.Log("[INFO] Finished unpacking base files!");
    }

    // P5
    public static async Task UnpackP5CPK(string directory) {
        if (!Directory.Exists(directory)) {
            ParallelLogger.Log($"[ERROR] Couldn't find {directory}. Please correct the file path in config.");
            return;
        }

        // Handle split ps3.cpk
        var ps3Cpk = Path.Combine(directory, "ps3.cpk");
        if (!File.Exists(ps3Cpk) &&
            File.Exists(Path.Combine(directory, "ps3.cpk.66600")) &&
            File.Exists(Path.Combine(directory, "ps3.cpk.66601")) &&
            File.Exists(Path.Combine(directory, "ps3.cpk.66602"))) {
            ParallelLogger.Log("[INFO] Combining ps3.cpk parts");
            using var output = File.Create(ps3Cpk);
            foreach (var part in new[] { "ps3.cpk.66600", "ps3.cpk.66601", "ps3.cpk.66602" }) {
                using var input = File.OpenRead(Path.Combine(directory, part));
                await input.CopyToAsync(output);
            }
        }

        var outputDir = Path.Combine(DataDir, "Original", "Persona 5");
        Directory.CreateDirectory(outputDir);

        var dataCsv = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", "filtered_data.csv");
        var ps3Csv = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", "filtered_ps3.csv");

        if (!File.Exists(dataCsv) || !File.Exists(ps3Csv)) {
            ParallelLogger.Log("[ERROR] Couldn't find CSV files used for unpacking in Dependencies/FilteredCpkCsv");
            return;
        }

        var dataCpk = Path.Combine(directory, "data.cpk");
        if (File.Exists(dataCpk)) {
            ParallelLogger.Log("[INFO] Extracting data.cpk");
            CriFsUnpack(dataCpk, outputDir, File.ReadAllLines(dataCsv));
        }
        else
            ParallelLogger.Log($"[ERROR] Couldn't find data.cpk in {directory}.");

        if (File.Exists(ps3Cpk)) {
            ParallelLogger.Log("[INFO] Extracting ps3.cpk");
            CriFsUnpack(ps3Cpk, outputDir, File.ReadAllLines(ps3Csv));
        }
        else
            ParallelLogger.Log($"[ERROR] Couldn't find ps3.cpk in {directory}.");

        ExtractWantedFiles(outputDir);

        ParallelLogger.Log("[INFO] Finished unpacking base files!");
    }

    // P5R (PS4)
    public static async Task UnpackP5RCPKs(string directory, string language, string version) {
        if (!Directory.Exists(directory)) {
            ParallelLogger.Log($"[ERROR] Couldn't find {directory}. Please correct the file path.");
            return;
        }

        var outputDir = Path.Combine(DataDir, "Original", "Persona 5 Royal (PS4)");
        Directory.CreateDirectory(outputDir);

        var dataCsv = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", "filtered_dataR.csv");
        var ps4Csv = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", "filtered_ps4R.csv");

        if (!File.Exists(dataCsv) || !File.Exists(ps4Csv)) {
            ParallelLogger.Log("[ERROR] Couldn't find CSV files used for unpacking in Dependencies/FilteredCpkCsv");
            return;
        }

        var dataRCpk = Path.Combine(directory, "dataR.cpk");
        if (File.Exists(dataRCpk)) {
            ParallelLogger.Log("[INFO] Extracting dataR.cpk");
            CriFsUnpack(dataRCpk, outputDir, File.ReadAllLines(dataCsv));
        }
        else
            ParallelLogger.Log($"[ERROR] Couldn't find dataR.cpk in {directory}.");

        var ps4RCpk = Path.Combine(directory, "ps4R.cpk");
        if (File.Exists(ps4RCpk)) {
            ParallelLogger.Log("[INFO] Extracting ps4R.cpk");
            CriFsUnpack(ps4RCpk, outputDir, File.ReadAllLines(ps4Csv));
        }
        else
            ParallelLogger.Log($"[ERROR] Couldn't find ps4R.cpk in {directory}.");

        // Localized CPK
        if (language != "English") {
            var localizedCpk = language switch {
                "French" => "dataR_F.cpk",
                "Italian" => "dataR_I.cpk",
                "German" => "dataR_G.cpk",
                "Spanish" => "dataR_S.cpk",
                _ => ""
            };
            if (!string.IsNullOrEmpty(localizedCpk)) {
                var localizedCsv = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", "filtered_dataR_Localized.csv");
                if (File.Exists(localizedCsv)) {
                    var fullPath = Path.Combine(directory, localizedCpk);
                    if (File.Exists(fullPath)) {
                        ParallelLogger.Log($"[INFO] Extracting {localizedCpk}");
                        CriFsUnpack(fullPath, outputDir, File.ReadAllLines(localizedCsv));
                    }
                    else
                        ParallelLogger.Log($"[ERROR] Couldn't find {localizedCpk} in {directory}.");
                }
            }
        }

        // Patch files for >= 1.02
        if (version == ">= 1.02") {
            var patchCsv = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", "filtered_patch2R.csv");
            if (File.Exists(patchCsv)) {
                var patchCpk = Path.Combine(directory, "patch2R.cpk");
                if (File.Exists(patchCpk)) {
                    ParallelLogger.Log("[INFO] Extracting patch2R.cpk");
                    CriFsUnpack(patchCpk, outputDir, File.ReadAllLines(patchCsv));
                }
                else
                    ParallelLogger.Log("[ERROR] Couldn't find patch2R.cpk in {directory}.");

                if (language != "English") {
                    var patchSuffix = language switch {
                        "French" => "_F",
                        "Italian" => "_I",
                        "German" => "_G",
                        "Spanish" => "_S",
                        _ => ""
                    };
                    if (!string.IsNullOrEmpty(patchSuffix)) {
                        var localPatchCsv = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", $"filtered_patch2R{patchSuffix}.csv");
                        var localPatchCpk = Path.Combine(directory, $"patch2R{patchSuffix}.cpk");
                        if (File.Exists(localPatchCsv) && File.Exists(localPatchCpk)) {
                            ParallelLogger.Log($"[INFO] Extracting patch2R{patchSuffix}.cpk");
                            CriFsUnpack(localPatchCpk, outputDir, File.ReadAllLines(localPatchCsv));
                        }
                    }
                }
            }
        }

        ExtractWantedFiles(outputDir);

        ParallelLogger.Log("[INFO] Finished unpacking base files!");
    }

    // P5R Switch
    public static async Task UnpackP5RSwitchCPKs(string directory, string language) {
        if (!Directory.Exists(directory)) {
            ParallelLogger.Log($"[ERROR] Couldn't find {directory}. Please correct the file path.");
            return;
        }

        var outputDir = Path.Combine(DataDir, "Original", "Persona 5 Royal (Switch)");
        Directory.CreateDirectory(outputDir);

        var patch1 = Path.Combine(directory, "PATCH1.CPK");
        if (File.Exists(patch1)) {
            ParallelLogger.Log("[INFO] Extracting PATCH1.CPK");
            CriFsUnpack(patch1, outputDir);
        }
        else
            ParallelLogger.Log($"[ERROR] Couldn't find PATCH1.CPK in {directory}.");

        var allUseu = Path.Combine(directory, "ALL_USEU.CPK");
        if (File.Exists(allUseu)) {
            ParallelLogger.Log("[INFO] Extracting ALL_USEU.CPK (This will take awhile)");
            CriFsUnpack(allUseu, outputDir);
        }
        else
            ParallelLogger.Log($"[ERROR] Couldn't find ALL_USEU.CPK in {directory}.");

        ExtractWantedFiles(outputDir);

        ParallelLogger.Log("[INFO] Finished unpacking base files!");
    }

    // P5R PC
    public static async Task UnpackP5RPCCPKs(string directory, string language) {
        if (!Directory.Exists(directory)) {
            ParallelLogger.Log($"[ERROR] Couldn't find {directory}. Please correct the file path.");
            return;
        }

        var outputDir = Path.Combine(DataDir, "Original", "Persona 5 Royal (PC)");
        Directory.CreateDirectory(outputDir);

        var cpkMakeC = Path.Combine(AppDir, "Dependencies", "CpkMakeC", "cpkmakec.exe");
        if (!File.Exists(cpkMakeC)) {
            // Fall back to CriFsLib for extraction
            ParallelLogger.Log("[INFO] cpkmakec not found, using CriFsLib for extraction");
            var baseCpk = Path.Combine(directory, "BASE.CPK");
            if (File.Exists(baseCpk)) {
                ParallelLogger.Log("[INFO] Extracting BASE.CPK (This will take awhile)");
                CriFsUnpack(baseCpk, outputDir);
            }
            else
                ParallelLogger.Log($"[ERROR] Couldn't find BASE.CPK in {directory}.");

            var localCpk = language switch {
                "English" => "EN.CPK",
                "French" => "FR.CPK",
                "Italian" => "IT.CPK",
                "German" => "DE.CPK",
                "Spanish" => "ES.CPK",
                _ => "EN.CPK"
            };
            var localPath = Path.Combine(directory, localCpk);
            if (File.Exists(localPath)) {
                ParallelLogger.Log($"[INFO] Extracting {localCpk} (This will take awhile)");
                CriFsUnpack(localPath, outputDir);
            }
            else
                ParallelLogger.Log($"[ERROR] Couldn't find {localCpk} in {directory}.");
        }
        else {
            // Use cpkmakec (Windows)
            var baseCpk = Path.Combine(directory, "BASE.CPK");
            if (File.Exists(baseCpk)) {
                ParallelLogger.Log("[INFO] Extracting BASE.CPK (This will take awhile)");
                await RunProcess(cpkMakeC, $"\"{baseCpk}\" -extract=\"{outputDir}\"");
            }

            var localCpk = language switch {
                "English" => "EN.CPK",
                "French" => "FR.CPK",
                "Italian" => "IT.CPK",
                "German" => "DE.CPK",
                "Spanish" => "ES.CPK",
                _ => "EN.CPK"
            };
            var localPath = Path.Combine(directory, localCpk);
            if (File.Exists(localPath)) {
                ParallelLogger.Log($"[INFO] Extracting {localCpk} (This will take awhile)");
                await RunProcess(cpkMakeC, $"\"{localPath}\" -extract=\"{outputDir}\"");
            }
        }

        ExtractWantedFiles(outputDir);

        ParallelLogger.Log("[INFO] Finished unpacking base files!");
    }

    // P4G Vita
    public static async Task UnpackP4GCPK(string cpk) {
        if (!File.Exists(cpk)) {
            ParallelLogger.Log($"[ERROR] Couldn't find {cpk}. Please correct the file path.");
            return;
        }

        var outputDir = Path.Combine(DataDir, "Original", "Persona 4 Golden (Vita)");
        Directory.CreateDirectory(outputDir);

        var csvPath = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", "filtered_p4gdata.csv");
        if (!File.Exists(csvPath)) {
            ParallelLogger.Log("[ERROR] Couldn't find CSV file used for unpacking in Dependencies/FilteredCpkCsv");
            return;
        }

        ParallelLogger.Log("[INFO] Extracting data.cpk");
        CriFsUnpack(cpk, outputDir, File.ReadAllLines(csvPath));

        ExtractWantedFiles(outputDir);

        ParallelLogger.Log("[INFO] Finished unpacking base files!");
    }

    // PQ
    public static async Task UnpackPQCPK(string cpk) {
        if (!File.Exists(cpk)) {
            ParallelLogger.Log($"[ERROR] Couldn't find {cpk}. Please correct the file path.");
            return;
        }

        var outputDir = Path.Combine(DataDir, "Original", "Persona Q");
        Directory.CreateDirectory(outputDir);

        var csvPath = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", "filtered_data_pq.csv");
        if (!File.Exists(csvPath)) {
            ParallelLogger.Log("[ERROR] Couldn't find CSV file used for unpacking in Dependencies/FilteredCpkCsv");
            return;
        }

        ParallelLogger.Log("[INFO] Extracting data.cpk");
        CriFsUnpack(cpk, outputDir, File.ReadAllLines(csvPath));

        ExtractWantedFiles(outputDir);

        ParallelLogger.Log("[INFO] Finished unpacking base files!");
    }

    // PQ2
    public static async Task UnpackPQ2CPK(string cpk) {
        if (!File.Exists(cpk)) {
            ParallelLogger.Log($"[ERROR] Couldn't find {cpk}. Please correct the file path.");
            return;
        }

        var outputDir = Path.Combine(DataDir, "Original", "Persona Q2");
        Directory.CreateDirectory(outputDir);

        var csvPath = Path.Combine(AppDir, "Dependencies", "FilteredCpkCsv", "filtered_data_pq2.csv");
        if (!File.Exists(csvPath)) {
            ParallelLogger.Log("[ERROR] Couldn't find CSV file used for unpacking in Dependencies/FilteredCpkCsv");
            return;
        }

        ParallelLogger.Log("[INFO] Extracting data.cpk");
        CriFsUnpack(cpk, outputDir, File.ReadAllLines(csvPath));

        ExtractWantedFiles(outputDir);

        ParallelLogger.Log("[INFO] Finished unpacking base files!");
    }

    private static void CriFsUnpack(string cpk, string dir, string[]? fileList = null) {
        using var fileStream = new FileStream(cpk, System.IO.FileMode.Open);
        using var reader = CriFsLib.Instance.CreateCpkReader(fileStream, true);
        var files = reader.GetFiles();
        fileStream.Close();

        bool extractAll = fileList == null;
        using var extractor = CriFsLib.Instance.CreateBatchExtractor<FileToExtract>(cpk, P5RCrypto.DecryptionFunction);
        for (int x = 0; x < files.Length; x++) {
            string filePath = string.IsNullOrEmpty(files[x].Directory)
                ? files[x].FileName
                : $"{files[x].Directory}/{files[x].FileName}";

            if (extractAll || fileList!.Contains(filePath)) {
                extractor.QueueItem(new FileToExtract(Path.Combine(dir, filePath), files[x]));
                ParallelLogger.Log($"[INFO] Extracting {filePath}");
            }
        }

        extractor.WaitForCompletion();
        ArrayRental.Reset();
    }

    private static string? FindPAKPack() {
        if (!OperatingSystem.IsWindows()) {
            var pakPack = Path.Combine(AppDir, "Dependencies", "AtlusFileSystemLibrary", "PAKPack");
            if (File.Exists(pakPack)) return pakPack;
        }
        var pakPackExe = Path.Combine(AppDir, "Dependencies", "AtlusFileSystemLibrary", "PAKPack.exe");
        if (File.Exists(pakPackExe)) return pakPackExe;

        return null;
    }

    private static void PAKPackCMD(string args) {
        var pakPack = FindPAKPack();
        if (pakPack == null) {
            ParallelLogger.Log("[ERROR] PAKPack.exe not found in Dependencies/PAKPack/");
            return;
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo {
            FileName = pakPack,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        process.Start();
        process.WaitForExit();
    }

    private static List<string> GetFileContents(string path) {
        var pakPack = FindPAKPack();
        if (pakPack == null) return new List<string>();

        string fileName = pakPack;
        string finalArgs = $"list \"{path}\"";

        var contents = new List<string>();
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo {
            FileName = fileName,
            Arguments = finalArgs,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        process.Start();
        while (!process.StandardOutput.EndOfStream) {
            var line = process.StandardOutput.ReadLine();
            if (line != null && !line.Contains(' '))
                contents.Add(line);
        }
        process.WaitForExit();
        return contents;
    }

    private static void ExtractWantedFiles(string directory) {
        if (!Directory.Exists(directory))
            return;

        if (FindPAKPack() == null) {
            ParallelLogger.Log("[WARNING] PAKPack not found, skipping archive extraction. Some files may be missing.");
            return;
        }

        var extensions = new[] { ".arc", ".bin", ".pac", ".pak", ".abin", ".gsd", ".tpc" };
        var files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(s => extensions.Any(ext => s.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

        foreach (string file in files) {
            List<string> contents = GetFileContents(file).Select(x => x.ToLower()).ToList();
            bool containersFound = contents.Exists(x =>
                x.EndsWith(".bin") || x.EndsWith(".pac") || x.EndsWith(".pak") ||
                x.EndsWith(".abin") || x.EndsWith(".arc"));

            if (contents.Exists(x =>
                x.EndsWith(".bf") || x.EndsWith(".bmd") || x.EndsWith(".pm1") ||
                x.EndsWith(".dat") || x.EndsWith(".ctd") || x.EndsWith(".ftd") ||
                x.EndsWith(".spd") || x.EndsWith(".acb") || x.EndsWith(".awb") || containersFound)) {
                ParallelLogger.Log($"[INFO] Unpacking {file}");
                PAKPackCMD($"unpack \"{file}\"");

                if (containersFound)
                    ExtractWantedFiles(Path.Combine(
                        Path.GetDirectoryName(file)!,
                        Path.GetFileNameWithoutExtension(file)));
            }
        }
    }

    private static async Task RunProcess(string fileName, string arguments) {
        var startInfo = new ProcessStartInfo {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync();
    }
}
