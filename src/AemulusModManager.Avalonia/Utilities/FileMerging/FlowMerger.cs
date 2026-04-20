using System.Collections.Generic;
using AemulusModManager.Avalonia.Utilities;
using System.IO;
using System.Linq;
using AemulusModManager.Utilities;

namespace AemulusModManager.Avalonia.Utilities.FileMerging;

/// <summary>
/// Cross-platform port of FlowMerger. Compiles .flow files to .bf using AtlusScriptCompiler CLI.
/// The compiled .bf is left in place inside the package directory so that BinMerger.Unpack
/// can later copy it to the output folder (Restart wipes output between FlowMerger and Unpack).
/// </summary>
public static class FlowMerger
{
    private static readonly string DataDir = AppPaths.DataDir;

    public static void Merge(List<string> ModList, string game, string language)
    {
        if (!ScriptCompiler.CompilerExists()) return;

        var compiledFiles = new List<string[]>();

        foreach (string dir in ModList)
        {
            var flowFiles = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                    .Where(s => (s.ToLower().EndsWith(".flow", System.StringComparison.OrdinalIgnoreCase) || s.ToLower().EndsWith(".bf", System.StringComparison.OrdinalIgnoreCase)) && !s.ToLower().EndsWith(".bf.flow", System.StringComparison.OrdinalIgnoreCase));

            string[]? aemIgnore = File.Exists(Path.Combine(dir, "Ignore.aem"))
                ? File.ReadAllLines(Path.Combine(dir, "Ignore.aem"))
                : null;

            foreach (string file in flowFiles)
            {
                string bf = Path.ChangeExtension(file, "bf");
                string filePath = ScriptCompiler.GetRelativePath(bf, dir, game);

                // If the current file is a bf check if it has a corresponding flow
                if (file.Equals(bf, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(Path.ChangeExtension(file, "flow")))
                        continue; // Skip bf if flow exists (flow will be compiled)
                    // Standalone bf - add as base, BinMerger.Unpack will copy it to output
                    compiledFiles.Add([filePath, dir, bf]);
                    continue;
                }

                if (aemIgnore != null && aemIgnore.Any(file.Contains))
                    continue;

                string[]? previousFileArr = compiledFiles.FindLast(p => p[0] == filePath);
                string? previousFile = previousFileArr?[2];

                // Copy a previously compiled bf so it can be merged with this flow
                if (previousFile != null)
                {
                    File.Copy(previousFile, bf, true);
                }
                else
                {
                    // Get the path of the file in original
                    string ogPath = Path.Combine(DataDir, "Original", game,
                        ScriptCompiler.GetRelativePath(bf, dir, game, false));
                    ogPath = FileManagement.GetActualCaseForFileName(ogPath);

                    if (File.Exists(ogPath))
                        File.Copy(ogPath, bf, true);
                    else
                    {
                        ParallelLogger.Log($"[INFO] Cannot find {ogPath}. Make sure you have unpacked the game's files if merging is needed.");
                        continue;
                    }
                }

                if (!ScriptCompiler.Compile(file, bf, game, language, Path.GetFileName(dir)))
                    continue;

                compiledFiles.Add([filePath, dir, bf]);
                // The compiled .bf stays in the package dir; BinMerger.Unpack copies it to output
            }
        }
    }
}
