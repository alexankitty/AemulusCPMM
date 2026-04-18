using System;
using System.IO;

namespace AemulusModManager.Avalonia.Utilities;

/// <summary>
/// Centralized path resolution for the application.
/// ExeDir: where the executable lives (Dependencies, Libraries, Charsets).
/// DataDir: where user data lives (Packages, Original, Config, Logs).
///   Linux:   $XDG_CONFIG_HOME/AemulusPackageManager or ~/.config/AemulusPackageManager
///   Windows: %APPDATA%\AemulusPackageManager
///   Fallback: ExeDir (if the platform dir can't be determined)
/// </summary>
public static class AppPaths
{
    private static string? _exeDir;
    private static string? _dataDir;

    /// <summary>
    /// Directory containing the executable and its bundled dependencies.
    /// Uses AppContext.BaseDirectory which works for both 'dotnet run' and published single-file apps.
    /// </summary>
    public static string ExeDir =>
        _exeDir ??= Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

    /// <summary>
    /// Directory for user-mutable data: Packages, Original, Config, Logs.
    /// </summary>
    public static string DataDir
    {
        get
        {
            if (_dataDir != null) return _dataDir;

            string? baseDir = null;

            if (OperatingSystem.IsWindows())
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrEmpty(appData))
                    baseDir = Path.Combine(appData, "AemulusPackageManager");
            }
            else
            {
                // Linux / macOS: prefer XDG_CONFIG_HOME, fallback to ~/.config
                var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (string.IsNullOrEmpty(xdg))
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (!string.IsNullOrEmpty(home))
                        xdg = Path.Combine(home, ".config");
                }
                if (!string.IsNullOrEmpty(xdg))
                    baseDir = Path.Combine(xdg, "AemulusPackageManager");
            }

            _dataDir = !string.IsNullOrEmpty(baseDir) ? baseDir : ExeDir;
            return _dataDir;
        }
    }

    // Convenience properties for commonly used subdirectories
    public static string ConfigDir => Path.Combine(DataDir, "Config");
    public static string PackagesDir => Path.Combine(DataDir, "Packages");
    public static string OriginalDir => Path.Combine(DataDir, "Original");
    public static string LogsDir => Path.Combine(DataDir, "Logs");
}
