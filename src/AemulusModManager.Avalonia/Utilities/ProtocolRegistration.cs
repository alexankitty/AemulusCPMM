using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace AemulusModManager.Avalonia.Utilities;

public static class ProtocolRegistration
{
    private static readonly string ProtocolName = "aemulus";
    private static readonly string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                                      RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                                      RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "unknown";
    public static void RegisterProtocol()
    {
        switch (platform)
        {
            case string s when s.StartsWith("win"):
                RegisterProtocolWindows();
                break;
            case string s when s.StartsWith("linux"):
                RegisterProtocolLinux();
                break;
            default:
                Console.WriteLine($"Protocol registration not supported on platform: {platform}");
                break;
        }
    }
    public static void RegisterProtocolWindows()
    {
        try
        {
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Classes\\{ProtocolName}");
            key.SetValue("", "URL:Aemulus Protocol");
            key.SetValue("URL Protocol", "");
            key.CreateSubKey(@"shell\open\command").SetValue("", $"\"{Process.GetCurrentProcess().MainModule.FileName}\" \"%1\"");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register protocol: {ex}");
        }
    }
    public static void RegisterProtocolLinux()
    {
        // Linux protocol registration typically involves creating a .desktop file and updating the MIME database.
        // This is a simplified example and may require additional steps for a complete implementation.
        string desktopFileContent = $"[Desktop Entry]\n"+
                                    $"Name=Aemulus Protocol Handler\n" +
                                    $"Exec={Process.GetCurrentProcess().MainModule.FileName} %u\n" +
                                    $"Type=Application\n" +
                                    $"MimeType=x-scheme-handler/{ProtocolName}\n";

        string desktopFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications", $"{ProtocolName}-handler.desktop");
        try
        {
            File.WriteAllText(desktopFilePath, desktopFileContent);
            // Update the MIME database (this may require additional permissions)
            Process.Start("update-desktop-database", $"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications")}");
            Process.Start("xdg-mime", $"default {ProtocolName}-handler.desktop x-scheme-handler/{ProtocolName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register protocol: {ex}");
        }
    }
    public static bool CheckRegistration()
    {
        switch (platform)
        {
            case string s when s.StartsWith("win"):
                return CheckRegistrationWindows();
            case string s when s.StartsWith("linux"):
                return CheckRegistrationLinux();
            default:
                Console.WriteLine($"Protocol registration check not supported on platform: {platform}");
                return false;
        }
    }

    public static bool CheckRegistrationWindows()
    {
        try
        {
            Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($"SOFTWARE\\Classes\\{ProtocolName}");
            if(key == null || key.GetValue("")?.ToString() != "URL:Aemulus Protocol")
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
    }

    public static bool CheckRegistrationLinux()
    {
        string desktopFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications", $"{ProtocolName}-handler.desktop");
        if (!File.Exists(desktopFilePath))
        {
            return false;
        }
        string desktopFileContent = File.ReadAllText(desktopFilePath);
        return desktopFileContent.Contains($"MimeType=x-scheme-handler/{ProtocolName}");
    }
}