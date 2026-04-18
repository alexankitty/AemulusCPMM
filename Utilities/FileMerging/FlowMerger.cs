using System.Collections.Generic;
using AemulusModManager.Avalonia.Utilities;
using System.IO;
using System.Linq;
using System.Reflection;
using AemulusModManager.Utilities;

namespace AemulusModManager.Avalonia.Utilities.FileMerging;

/// <summary>
/// Cross-platform port of FlowMerger. Compiles .flow files to .bf using AtlusScriptCompiler CLI.
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
                .Where(s => (s.ToLower().EndsWith(".flow") || s.ToLower().EndsWith(".bf"))
                    && !s.ToLower().EndsWith(".bf.flow"));

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
                    // Standalone bf — add as base
                    compiledFiles.Add(new[] { filePath, dir, bf });
                    continue;
                }

                string[]? previousFileArr = compiledFiles.FindLast(p => p[0] == filePath);
                string? previousFile = previousFileArr?[2];

                // Copy a previously compiled bf so it can be merged
                if (previousFile != null)
                {
                    File.Copy(previousFile, bf, true);
                }
                else
                {
                    // Get the path of the file in original
                    string ogPath = Path.Combine(DataDir, "Original", game,
                        ScriptCompiler.GetRelativePath(bf, dir, game, false));

                    if (aemIgnore != null && aemIgnore.Any(file.Contains))
                        continue;
                    else if (File.Exists(ogPath))
                        File.Copy(ogPath, bf, true);
                    else
                    {
                        ParallelLogger.Log($"[INFO] Cannot find {ogPath}. Make sure you have unpacked the game's files if merging is needed");
                        continue;
                    }
                }

                if (!ScriptCompiler.Compile(file, bf, game, language, Path.GetFileName(dir)))
                    continue;

                compiledFiles.Add(new[] { filePath, dir, bf });
            }
        }
    }
}
