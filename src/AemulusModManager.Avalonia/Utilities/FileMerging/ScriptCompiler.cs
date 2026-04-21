using System;
using AemulusModManager.Avalonia.Utilities;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AemulusModManager.Utilities;

namespace AemulusModManager.Avalonia.Utilities.FileMerging;

/// <summary>
/// Cross-platform wrapper for AtlusScriptCompiler.exe and PM1MessageScriptEditor.exe.
/// On Linux/macOS, runs via mono.
/// </summary>
public static class ScriptCompiler
{
    private static readonly string AppDir = AppPaths.ExeDir;
    private static readonly string DataDir = AppPaths.DataDir;

    private static string? _monoPath;
    private static bool _monoChecked;
    private static string CompilerPath => Path.Combine(AppDir, "Dependencies", "AtlusScriptCompiler", "AtlusScriptCompiler.exe");
    // Native self-contained linux-x64 binary (no .exe, no mono required)
    private static string PM1Path => Path.Combine(AppDir, "Dependencies", "PM1MessageScriptEditor", "PM1MessageScriptEditor.exe");

    private static readonly Dictionary<string, (string Library, string Encoding, string FlowFormat, string MsgFormat)> GameInfo = new()
    {
        ["Persona 4 Golden"]        = ("p4g", "P4", "V1", "V1"),
        ["Persona 4 Golden (Vita)"] = ("p4g", "P4", "V1", "V1"),
        ["Persona 3 FES"]           = ("p3f", "P3", "V1", "V1"),
        ["Persona 5"]               = ("p5",  "P5", "V3BE", "V1BE"),
        ["Persona 3 Portable"]      = ("p3p", "P3", "V1", "V1"),
        ["Persona 5 Royal (PS4)"]   = ("p5r", "P5", "V3BE", "V1BE"),
        ["Persona 5 Royal (Switch)"]= ("p5r", "P5", "V3BE", "V1BE"),
        ["Persona Q2"]              = ("pq2", "SJ", "V2", "V1"),
    };

    private static string? FindMono()
    {
        if (_monoChecked) return _monoPath;
        _monoChecked = true;
        if (OperatingSystem.IsWindows()) return null;
        try
        {
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "which", Arguments = "mono",
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            });
            if (proc != null)
            {
                var path = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(path))
                    _monoPath = path;
            }
        }
        catch { }
        return _monoPath;
    }

    public static void RunExe(string exePath, string args)
    {
        string fileName;
        string finalArgs;
        if (!OperatingSystem.IsWindows())
        {
            // Prefer native self-contained binary (strip .exe → AtlusScriptCompiler)
            var nativePath = Path.Combine(Path.GetDirectoryName(exePath)!, Path.GetFileNameWithoutExtension(exePath));
            if (File.Exists(nativePath))
            {
                fileName = nativePath;
                finalArgs = args;
            }
            else
            {
                ParallelLogger.Log($"[INFO] Native binary at {nativePath} not found. Attempting to run {exePath} via mono.");
                var mono = FindMono();
                if (mono == null)
                {
                    ParallelLogger.Log("[ERROR] Neither a native binary nor mono was found for this tool on Linux.");
                    return;
                }
                fileName = mono;
                finalArgs = $"\"{exePath}\" {args}";
            }
        }
        else
        {
            fileName = exePath;
            finalArgs = args;
        }
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = finalArgs,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        
        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        //if (!string.IsNullOrWhiteSpace(stdout))
            //ParallelLogger.Log($"[INFO] {Path.GetFileName(exePath)}: {stdout.Trim()}");
        if (!string.IsNullOrWhiteSpace(stderr))
            ParallelLogger.Log($"[ERROR] {Path.GetFileName(exePath)}: {stderr.Trim()}");
        if (process.ExitCode != 0)
            ParallelLogger.Log($"[ERROR] {Path.GetFileName(exePath)} exited with code {process.ExitCode}");
    }

    /// <summary>
    /// Compile a .flow or .msg file to .bf or .bmd using AtlusScriptCompiler CLI.
    /// </summary>
    public static bool Compile(string inFilePath, string outFile, string game, string language, string modName = "")
    {
        if (!File.Exists(inFilePath))
            return false;
        if (!GameInfo.TryGetValue(game, out var info))
        {
            ParallelLogger.Log($"[ERROR] No compiler info for game {game}");
            return false;
        }

        DateTime lastModified;
        try { lastModified = File.GetLastWriteTime(outFile); }
        catch (Exception e)
        {
            ParallelLogger.Log($"[ERROR] Error getting last write time for {outFile}: {e.Message}");
            return false;
        }

        ParallelLogger.Log($"[INFO] Compiling {inFilePath}");
        string extension = Path.GetExtension(outFile).ToLowerInvariant();

        if (extension == ".pm1")
        {
            RunExe(PM1Path, $"\"{inFilePath}\"");
        }
        else if (extension == ".bf")
        {
            var encoding = info.Encoding;
            var outFormat = info.FlowFormat;

            // Persona 5 bmds have a different outformat than their bfs
            if ((game == "Persona 5" || game == "Persona 5 Royal (PS4)" || game == "Persona 5 Royal (Switch)")
                && Path.GetExtension(inFilePath).ToLowerInvariant() == ".msg")
                outFormat = "V1BE";
            if (game == "Persona Q2" && Path.GetExtension(inFilePath).ToLowerInvariant() == ".msg")
                outFormat = "V1";

            // Use EFIGS encoding for non-English P5R
            if ((game == "Persona 5 Royal (PS4)" || game == "Persona 5 Royal (Switch)")
                && language != null && language != "English")
                encoding = "P5R_EFIGS";

            var args = $"\"{inFilePath}\" -Compile -OutFormat {outFormat} -Library {info.Library} -Encoding {encoding} -Out \"{outFile}\" -Hook";
            RunExe(CompilerPath, args);
        }
        else if (extension == ".bmd")
        {
            var encoding = info.Encoding;
            if ((game == "Persona 5 Royal (PS4)" || game == "Persona 5 Royal (Switch)")
                && language != null && language != "English")
                encoding = "P5R_EFIGS";

            var args = $"\"{inFilePath}\" -Compile -OutFormat {info.MsgFormat} -Library {info.Library} -Encoding {encoding} -Out \"{outFile}\"";
            RunExe(CompilerPath, args);
        }
        else
        {
            ParallelLogger.Log($"[ERROR] {extension} is not a supported file type");
            return false;
        }

        // Check if the file was written to (successfully compiled)
        if (File.Exists(outFile) && File.GetLastWriteTime(outFile) > lastModified)
        {
            ParallelLogger.Log($"[INFO] Finished compiling {inFilePath}");
            return true;
        }
        else
        {
            var logFile = Path.Combine(AppDir, "AtlusScriptCompiler.log");
            var newLog = Path.Combine(DataDir, "Logs", $"{modName}{(modName == "" ? "" : " - ")}{Path.GetFileName(inFilePath)}.log");
            if (File.Exists(logFile))
            {
                Directory.CreateDirectory(Path.Combine(DataDir, "Logs"));
                if (File.Exists(newLog)) File.Delete(newLog);
                File.Move(logFile, newLog);
            }
            ParallelLogger.Log($"[ERROR] Error compiling {inFilePath}. Check {newLog} for details.");
            return false;
        }
    }

    /// <summary>
    /// Decompile a .bf to .flow or .bmd to .msg using AtlusScriptCompiler CLI.
    /// </summary>
    public static bool Decompile(string inFilePath, string game, string language)
    {
        if (!File.Exists(inFilePath))
            return false;
        if (!GameInfo.TryGetValue(game, out var info))
        {
            ParallelLogger.Log($"[ERROR] No compiler info for game {game}");
            return false;
        }

        var encoding = info.Encoding;
        if ((game == "Persona 5 Royal (PS4)" || game == "Persona 5 Royal (Switch)")
            && language != null && language != "English")
            encoding = "P5R_EFIGS";

        var ext = Path.GetExtension(inFilePath).ToLowerInvariant();
        string outFormat;
        if (ext == ".bf")
            outFormat = info.FlowFormat;
        else if (ext == ".bmd")
            outFormat = info.MsgFormat;
        else
        {
            ParallelLogger.Log($"[ERROR] Cannot decompile {ext} files");
            return false;
        }

        var args = $"\"{inFilePath}\" -Decompile -Library {info.Library} -Encoding {encoding}";
        RunExe(CompilerPath, args);

        var expectedOut = ext == ".bmd"
            ? Path.ChangeExtension(inFilePath, ".msg")
            : Path.ChangeExtension(inFilePath, ".flow");

        return File.Exists(expectedOut);
    }

    /// <summary>
    /// Gets the path for a file relative to the game's file system.
    /// Cross-platform version using Path.DirectorySeparatorChar.
    /// </summary>
    public static string GetRelativePath(string file, string dir, string game, bool removeData = true)
    {
        var folders = new List<string>(file.Split(Path.DirectorySeparatorChar));
        int idx = folders.IndexOf(Path.GetFileName(dir)) + 1;
        if (game == "Persona 4 Golden" && removeData) idx++;
        folders = folders.Skip(idx).ToList();
        return Path.Combine(folders.ToArray());
    }

    /// <summary>
    /// Parse messages from a .msg file (decompiled text format).
    /// </summary>
    public static Dictionary<string, string>? GetMessages(string file, string fileType)
    {
        try
        {
            string messagePattern = @"(\[.+ .+\])\s+((?:\[.*\s+?)+)";
            string text = File.ReadAllText(file).Replace("[x 0x80 0x80]", " ");
            var rg = new Regex(messagePattern);
            var matches = rg.Matches(text);
            var messages = new Dictionary<string, string>();
            foreach (Match match in matches)
                messages[match.Groups[1].Value] = match.Groups[2].Value;
            return messages;
        }
        catch (Exception e)
        {
            ParallelLogger.Log($"[ERROR] Error reading {file}: {e.Message}. Cancelling {fileType} merging");
        }
        return null;
    }

    /// <summary>
    /// Text-level merge of two MSG files against an original, then recompile.
    /// Used for BMD and PM1 merging.
    /// </summary>
    public static void MergeFiles(string game, string[] files, Dictionary<string, string>[] messages,
        Dictionary<string, string> ogMessages, string language)
    {
        var changedMessages = new Dictionary<string, string>();
        foreach (var ogMessage in ogMessages)
        {
            foreach (var messageArr in messages)
            {
                if (messageArr.TryGetValue(ogMessage.Key, out string? messageContent))
                {
                    if (messageContent != ogMessage.Value)
                    {
                        changedMessages.Remove(ogMessage.Key);
                        changedMessages[ogMessage.Key] = messageContent;
                    }
                }
            }
        }

        // Get any completely new messages from the lower priority file
        foreach (var m in messages[0])
        {
            if (!ogMessages.ContainsKey(m.Key) && !messages[1].ContainsKey(m.Key))
                changedMessages[m.Key] = m.Value;
        }

        if (changedMessages.Count <= 0)
            return;

        string msgFile = Path.ChangeExtension(files[1], "msg");
        string fileContent;
        try { fileContent = File.ReadAllText(msgFile); }
        catch (Exception e)
        {
            ParallelLogger.Log($"[ERROR] Error reading {msgFile}: {e.Message}");
            return;
        }

        foreach (var message in changedMessages)
        {
            if (!ogMessages.TryGetValue(message.Key, out string? ogMessage))
                fileContent += $"{message.Key}\r\n{message.Value}\r\n";
            else
                fileContent = fileContent.Replace($"{message.Key}\r\n{ogMessage}", $"{message.Key}\r\n{message.Value}");
        }

        try { File.Copy(files[1], files[1] + ".back", true); }
        catch (Exception e)
        {
            ParallelLogger.Log($"[ERROR] Error backing up {files[1]}: {e.Message}");
            return;
        }

        try { File.WriteAllText(msgFile, fileContent); }
        catch (Exception e)
        {
            ParallelLogger.Log($"[ERROR] Error writing changes to {msgFile}: {e.Message}");
            return;
        }
        Compile(msgFile, files[1], game, language);
    }

    /// <summary>
    /// Restore .*.back files to their original names after merging.
    /// </summary>
    public static void RestoreBackups(List<string> modList)
    {
        foreach (string modDir in modList)
        {
            string[] bakFiles = Directory.GetFiles(modDir, "*.*.back", SearchOption.AllDirectories);
            foreach (string file in bakFiles)
            {
                string newFile = file[..^5]; // Remove ".back"
                if (File.Exists(newFile))
                    File.Delete(newFile);
                File.Move(file, newFile);
            }
        }
    }
}
