using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
namespace AemulusModManager.Avalonia.Utilities;

public static class FileManagement
{
    public static string ValidatePathCaseInsensitive(string pathAndFileName)
    {
        string resultFileName = pathAndFileName;
        if (OperatingSystem.IsWindows() && (File.Exists(pathAndFileName) || Directory.Exists(pathAndFileName)))
            return pathAndFileName; // File exists as is, return it
        if(File.Exists(pathAndFileName) || Directory.Exists(pathAndFileName))
            return pathAndFileName; // File exists as is, return it

        string file = Path.GetFileName(pathAndFileName);
        string[] directories = Path.GetDirectoryName(Path.Combine(pathAndFileName))?
                                    .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries) ?? [];

        string finalPath = Path.GetPathRoot(pathAndFileName) ?? "";
        string parentDirectory = Path.GetDirectoryName(pathAndFileName) ?? "";

        foreach (string dir in directories)
        {
            IEnumerable<string> foundDirs = Directory.EnumerateDirectories(finalPath)
            .Where(s => Path.GetFileName(s).Equals(dir, StringComparison.OrdinalIgnoreCase));
            if (foundDirs.Any())
            {
                if (foundDirs.Count() > 1)
                {
                    // More than two directories with the same name but different case spelling found
                    throw new Exception("Ambiguous Directory reference for " + dir);
                }
                else
                {
                    finalPath = foundDirs.First();
                }
            }
            else
            {
                return null; // Directory not found
            }
        }

        IEnumerable<string> foundFiles = Directory.EnumerateFiles(finalPath)
            .Where(s => Path.GetFileName(s).Equals(file, StringComparison.OrdinalIgnoreCase));
        if (!foundFiles.Any())
        {
            foundFiles = Directory.EnumerateDirectories(finalPath)
            .Where(s => Path.GetFileName(s).Equals(file, StringComparison.OrdinalIgnoreCase));//Fall back to directory check
        }

        if (foundFiles.Any())
        {
            if (foundFiles.Count() > 1)
            {
                // More than two files with the same name but different case spelling found
                throw new Exception("Ambiguous File reference for " + pathAndFileName);
            }
            else
            {
                resultFileName = foundFiles.First();
            }
        }
        else
        {
            return null; // File not found
        }

        return resultFileName;
    }
}