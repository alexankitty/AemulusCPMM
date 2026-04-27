using System.Collections.Generic;
using AemulusModManager.Avalonia.Utilities;
using System;
using System.IO;
using System.Linq;
using AemulusModManager.Utilities;
using System.Text.RegularExpressions;
using System.Reflection.Metadata.Ecma335;
using AemulusModManager.Avalonia.Utilities;

namespace AemulusModManager.Avalonia.Utilities.FileMerging;

/// <summary>
/// Cross-platform port of FlowMerger. Compiles .flow files to .bf using AtlusScriptCompiler CLI.
/// The compiled .bf is left in place inside the package directory so that BinMerger.Unpack
/// can later copy it to the output folder (Restart wipes output between FlowMerger and Unpack).
/// </summary>
public static class FlowMerger {
    private static readonly string DataDir = AppPaths.DataDir;

    public static void Merge(List<string> ModList, string game, string language) {

        var compiledFiles = new List<string[]>();

        foreach (string dir in ModList) {
            Regex pattern = new Regex(@"^.*\.(flow|bf)$", RegexOptions.IgnoreCase);
            var flowFiles = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                    .Where(s => pattern.IsMatch(s));

            string[]? aemIgnore = FileManagement.ValidatePathCaseInsensitive(Path.Combine(dir, "Ignore.aem")) != null
                ? File.ReadAllLines(FileManagement.ValidatePathCaseInsensitive(Path.Combine(dir, "Ignore.aem")))
                : null;

            HashSet<string> importedFiles = new HashSet<string>();

            foreach (string file in flowFiles) {
                if (Path.GetExtension(file).Equals(".bf", StringComparison.OrdinalIgnoreCase))
                    continue; // Skip bf files in import search
                importedFiles.UnionWith(GetImportedFiles(file));
            }

            foreach (string file in flowFiles) {
                if (importedFiles.Contains(file)) {
                    ParallelLogger.Log($"[DEBUG] Skipping {file} since it's imported by another flow file.");
                    continue;
                }

                ParallelLogger.Log($"[INFO] Processing {file}...");

                string bf = Path.ChangeExtension(file, "bf");
                string filename = Path.GetFileNameWithoutExtension(file);
                string dirname = Path.GetDirectoryName(file) ?? "";
                string filePath = ScriptCompiler.GetRelativePath(bf, dir, game);

                // If the current file is a bf check if it has a corresponding flow
                if (file.Equals(bf, StringComparison.OrdinalIgnoreCase)) {

                    if (File.Exists(FileManagement.ValidatePathCaseInsensitive(Path.ChangeExtension(file, "flow"))))
                        continue; // Skip bf if flow exists (flow will be compiled)
                    // Standalone bf - add as base, BinMerger.Unpack will copy it to output
                    compiledFiles.Add([filePath, dir, file]);
                    continue;
                }

                if (aemIgnore != null && aemIgnore.Any(file.Contains))
                    continue;

                string[]? previousFileArr = compiledFiles.FindLast(p => p[0] == filePath);
                string? previousFile = previousFileArr?[2];

                // Get the path of the file in original
                string ogPath = Path.Combine(DataDir, "Original", game,
                    ScriptCompiler.GetRelativePath(bf, dir, game, false));
                ogPath = FileManagement.ValidatePathCaseInsensitive(ogPath);
                string ogPathExt = Path.GetExtension(ogPath);
                string newBf = Path.Combine(dirname, filename + ogPathExt);

                // Copy a previously compiled bf so it can be merged with this flow
                if (previousFile != null) {
                    File.Copy(previousFile, bf, true);
                }
                else {
                    if (ogPath != null)
                        File.Copy(ogPath, newBf, true);
                    else {
                        ParallelLogger.Log($"[INFO] Cannot find {ogPath}. Make sure you have unpacked the game's files if merging is needed.");
                        continue;
                    }
                }

                if (!ScriptCompiler.Compile(file, newBf, game, language, Path.GetFileName(dir)))
                    continue;

                compiledFiles.Add([filePath, dir, newBf]);
                // The compiled .bf stays in the package dir; BinMerger.Unpack copies it to output
            }
        }
    }

    public static HashSet<string> GetImportedFiles(string flowPath) {
        var directory = Path.GetDirectoryName(flowPath) ?? "";
        HashSet<string> importedFiles = new HashSet<string>();
        if (!File.Exists(flowPath)) {
            ParallelLogger.Log($"[ERROR] Flow file not found: {flowPath}");
            return importedFiles;
        }
        Regex importPattern = new Regex(@"(?:\s*import*'|"")([^""'()]+.flow)");
        foreach (string line in File.ReadLines(flowPath)) {
            Match match = importPattern.Match(line);
            if (match.Success) {
                importedFiles.Add(Path.Combine(directory, match.Groups[1].Value));
            }
        }
        return importedFiles;
    }
}
