using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace AemulusModManager.Avalonia.Utilities;

public static class FileManagement
{
    public static string GetActualCaseForFileName(string pathAndFileName)
{
    string directory = Path.GetDirectoryName(pathAndFileName) ?? string.Empty;
    if(string.IsNullOrWhiteSpace(directory))
    {
        throw new ArgumentException("Invalid path: " + pathAndFileName);
    }
    string pattern = Path.GetFileName(pathAndFileName);
    string resultFileName;

    // Enumerate all files in the directory, using the file name as a pattern
    // This will list all case variants of the filename even on file systems that
    // are case sensitive
    IEnumerable<string> foundFiles = Directory.EnumerateFiles(directory)
        .Where(s => Path.GetFileName(s).Equals(pattern, StringComparison.OrdinalIgnoreCase));

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
        throw new FileNotFoundException("File not found" + pathAndFileName, pathAndFileName);
    }

    return resultFileName;
    }
}