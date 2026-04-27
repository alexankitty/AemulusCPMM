using System.Collections.Generic;
using AemulusModManager.Avalonia.Utilities;
using System.IO;
using System.Reflection;
using AemulusModManager.Utilities;

namespace AemulusModManager.Avalonia.Utilities.FileMerging;

/// <summary>
/// Cross-platform port of BmdMerger. Uses CLI decompile/recompile for BMD merging
/// instead of in-process AtlusScriptLibrary.
/// </summary>
public static class BmdMerger {
    private static readonly string DataDir = AppPaths.DataDir;

    public static void Merge(List<string> ModList, string game, string language) {

        var foundBmds = new List<string[]>();

        foreach (string dir in ModList) {
            string[] bmdFiles = Directory.GetFiles(dir, "*.bmd", SearchOption.AllDirectories);
            foreach (string file in bmdFiles) {
                string filePath = ScriptCompiler.GetRelativePath(file, dir, game);
                string[]? previousFileArr = foundBmds.FindLast(p => p[0] == filePath);
                string? previousFile = previousFileArr?[2];

                // Merge bmds if there are two with the same relative path
                if (previousFile != null) {
                    string ogPath = Path.Combine(DataDir, "Original", game,
                        ScriptCompiler.GetRelativePath(file, dir, game, false));
                    MergeBmds(new[] { previousFile, file }, ogPath, game, language);
                }
                foundBmds.Add(new[] { filePath, dir, file });
            }
        }
    }

    private static void MergeBmds(string[] bmds, string ogPath, string game, string language) {
        if (!File.Exists(ogPath)) {
            ParallelLogger.Log($"[WARNING] Cannot find {ogPath}. Make sure you have unpacked the game's files if merging is needed.");
            return;
        }

        // Decompile the original and both mod BMDs to MSG text
        var ogMessages = DecompileAndGetMessages(ogPath, game, language);
        var messages0 = DecompileAndGetMessages(bmds[0], game, language);
        var messages1 = DecompileAndGetMessages(bmds[1], game, language);

        if (ogMessages == null || messages0 == null || messages1 == null)
            return;

        // Text-level merge using the shared MergeFiles utility
        ScriptCompiler.MergeFiles(game, bmds, new[] { messages0, messages1 }, ogMessages, language);
    }

    private static Dictionary<string, string>? DecompileAndGetMessages(string bmdPath, string game, string language) {
        if (!File.Exists(bmdPath)) {
            ParallelLogger.Log($"[ERROR] BMD file not found: {bmdPath}");
            return null;
        }

        // Decompile BMD to MSG
        if (!ScriptCompiler.Decompile(bmdPath, game, language)) {
            ParallelLogger.Log($"[ERROR] Failed to decompile {bmdPath}");
            return null;
        }

        var msgPath = Path.ChangeExtension(bmdPath, ".msg");
        return ScriptCompiler.GetMessages(msgPath, "bmd");
    }
}
