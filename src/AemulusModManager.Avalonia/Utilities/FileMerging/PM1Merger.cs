using System.Collections.Generic;
using AemulusModManager.Avalonia.Utilities;
using System.IO;
using System.Reflection;
using AemulusModManager.Utilities;
using System.Runtime.InteropServices;

namespace AemulusModManager.Avalonia.Utilities.FileMerging;

/// <summary>
/// Cross-platform port of PM1Merger.
/// </summary>
public static class PM1Merger
{
    private static readonly string AppDir = AppPaths.ExeDir;
    private static readonly string DataDir = AppPaths.DataDir;
    private static string PM1Path => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(AppDir, "Dependencies", "PM1MessageScriptEditor", "AtlusPM1MessageScriptEditor.exe") : 
                                     Path.Combine(AppDir, "Dependencies", "PM1MessageScriptEditor", "AtlusPM1MessageScriptEditor") + "\"";

    public static void Merge(List<string> ModList, string game, string language)
    {

        var foundFiles = new List<string[]>();

        foreach (string dir in ModList)
        {
            string[] pm1Files = Directory.GetFiles(dir, "*.pm1", SearchOption.AllDirectories);
            foreach (string file in pm1Files)
            {
                string filePath = ScriptCompiler.GetRelativePath(file, dir, game);
                string[]? previousFileArr = foundFiles.FindLast(p => p[0] == filePath);
                string? previousFile = previousFileArr?[2];

                // Merge pm1s if there are two with the same relative path
                if (previousFile != null)
                {
                    string ogPath = Path.Combine(DataDir, "Original", game,
                        ScriptCompiler.GetRelativePath(file, dir, game, false));
                    MergePm1s(new[] { previousFile, file }, ogPath, game, language);
                }
                foundFiles.Add(new[] { filePath, dir, file });
            }
        }
    }

    private static void MergePm1s(string[] files, string ogPath, string game, string language)
    {
        if (!File.Exists(ogPath))
        {
            ParallelLogger.Log($"[WARNING] Cannot find {ogPath}. Make sure you have unpacked the game's files if merging is needed");
            return;
        }

        var messages0 = GetPm1Messages(files[0], game);
        var messages1 = GetPm1Messages(files[1], game);
        var ogMessages = GetPm1Messages(ogPath, game);

        if (messages0 == null || messages1 == null || ogMessages == null)
            return;

        ScriptCompiler.MergeFiles(game, files, new[] { messages0, messages1 }, ogMessages, language);
    }

    private static Dictionary<string, string>? GetPm1Messages(string file, string game)
    {
        // Decompile the pm1 to a msg using PM1MessageScriptEditor
        ScriptCompiler.RunExe(PM1Path, $"\"{file}\"");
        string msgFile = Path.ChangeExtension(file, "msg");
        return ScriptCompiler.GetMessages(msgFile, "pm1");
    }
}
