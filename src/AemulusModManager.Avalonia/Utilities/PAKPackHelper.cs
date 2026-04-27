using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using AemulusModManager.Utilities;

namespace AemulusModManager;

/// <summary>
/// Cross-platform wrapper for PAKPack.exe operations.
/// On Linux/macOS, runs PAKPack via mono.
/// </summary>
public static class PAKPackHelper {
    private static string AppDir => AemulusModManager.Avalonia.Utilities.AppPaths.ExeDir;

    private static string? _pakPackPath;
    private static string? _monoPath;
    private static bool _initialized;

    private static void Initialize() {
        if (_initialized) return;
        _initialized = true;

        var pakPackExe = Path.Combine(AppDir, "Dependencies", "PAKPack", "PAKPack.exe");
        if (File.Exists(pakPackExe))
            _pakPackPath = pakPackExe;

        if (!OperatingSystem.IsWindows()) {
            try {
                var proc = Process.Start(new ProcessStartInfo {
                    FileName = "which",
                    Arguments = "mono",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc != null) {
                    var path = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit();
                    if (proc.ExitCode == 0 && !string.IsNullOrEmpty(path))
                        _monoPath = path;
                }
            }
            catch { }
        }
    }

    public static bool IsAvailable() {
        Initialize();
        if (_pakPackPath == null) return false;
        if (!OperatingSystem.IsWindows() && _monoPath == null) return false;
        return true;
    }

    public static void PAKPackCMD(string args) {
        Initialize();
        if (_pakPackPath == null) {
            ParallelLogger.Log("[ERROR] PAKPack.exe not found in Dependencies/PAKPack/");
            return;
        }

        string fileName;
        string finalArgs;
        if (!OperatingSystem.IsWindows()) {
            if (_monoPath == null) {
                ParallelLogger.Log("[ERROR] mono is required to run PAKPack on Linux. Install mono-runtime.");
                return;
            }
            fileName = _monoPath;
            finalArgs = $"\"{_pakPackPath}\" {args}";
        }
        else {
            fileName = _pakPackPath;
            finalArgs = args;
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo {
            FileName = fileName,
            Arguments = finalArgs,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (!string.IsNullOrWhiteSpace(stderr))
            ParallelLogger.Log($"[WARNING] PAKPack stderr: {stderr.Trim()}");
        if (process.ExitCode != 0)
            ParallelLogger.Log($"[WARNING] PAKPack exited with code {process.ExitCode} for args: {args}");
    }

    public static List<string> GetFileContents(string path) {
        Initialize();
        if (_pakPackPath == null) return new List<string>();

        string fileName;
        string finalArgs;
        if (!OperatingSystem.IsWindows()) {
            if (_monoPath == null) return new List<string>();
            fileName = _monoPath;
            finalArgs = $"\"{_pakPackPath}\" list \"{path}\"";
        }
        else {
            fileName = _pakPackPath;
            finalArgs = $"list \"{path}\"";
        }

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
}
