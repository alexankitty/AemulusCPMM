using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace AemulusModManager.Avalonia.Utilities;

public static class ProtocolRegistration {
    private static readonly string ProtocolName = "aemulus";

    public static void RegisterProtocol() {
#if WINDOWS
        try
        {
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $"SOFTWARE\\Classes\\{ProtocolName}"
            );
            key.SetValue("", "URL:Aemulus Protocol");
            key.SetValue("URL Protocol", "");
            key.CreateSubKey(@"shell\open\command")
                .SetValue("", $"\"{Process.GetCurrentProcess().MainModule.FileName}\" \"%1\"");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register protocol: {ex}");
        }
#else
        string desktopFileContent =
            $"[Desktop Entry]\n"
            + $"Name=Aemulus Protocol Handler\n"
            + $"Exec={Process.GetCurrentProcess().MainModule.FileName} %u\n"
            + $"Type=Application\n"
            + $"MimeType=x-scheme-handler/{ProtocolName}\n";

        string desktopFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "applications",
            $"{ProtocolName}-handler.desktop"
        );
        try {
            File.WriteAllText(desktopFilePath, desktopFileContent);
            // Update the MIME database (this may require additional permissions)
            Process.Start(
                "update-desktop-database",
                $"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications")}"
            );
            Process.Start(
                "xdg-mime",
                $"default {ProtocolName}-handler.desktop x-scheme-handler/{ProtocolName}"
            );
        }
        catch (Exception ex) {
            Console.WriteLine($"Failed to register protocol: {ex}");
        }
#endif
    }

    public static bool CheckRegistration() {
#if WINDOWS

        try
        {
            Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                $"SOFTWARE\\Classes\\{ProtocolName}"
            );
            if (key == null || key.GetValue("")?.ToString() != "URL:Aemulus Protocol")
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to check protocol registration: {ex}");
            return false;
        }

        return true;

#else

        string desktopFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "applications",
            $"{ProtocolName}-handler.desktop"
        );

        if (!File.Exists(desktopFilePath)) {
            return false;
        }

        string desktopFileContent = File.ReadAllText(desktopFilePath);
        return desktopFileContent.Contains($"MimeType=x-scheme-handler/{ProtocolName}");
#endif
    }
}
