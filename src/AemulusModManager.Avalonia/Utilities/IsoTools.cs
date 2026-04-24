using DiscUtils.Iso9660;
using AemulusModManager.Utilities;
using System.IO;
using System.Text;
namespace AemulusModManager.Avalonia.Utilities
{
    public static class IsoTools
    {
        public static class IsoBuilder
        {
            public static void CreateISO(string sourceDirectory, string outputIsoPath, Identifiers identifiers)
            {
                ParallelLogger.Log($"Creating ISO from {sourceDirectory} to {outputIsoPath}");
                using (var isoStream = File.Create(outputIsoPath))
                {
                    var builder = new CDBuilder
                    {
                        VolumeIdentifier = identifiers.VolumeIdentifier ?? "UNTITLED",
                        UseJoliet = true,
                    };
                    BuildDirectory(builder, sourceDirectory, sourceDirectory);
                    builder.Build(isoStream);
                }
                Patcher.PatchIdentifiers(outputIsoPath, identifiers);
            }
            public static void BuildDirectory(CDBuilder builder, string rootDirectory, string currentDirectory)
            {
                foreach (var file in Directory.GetFiles(currentDirectory))
                {
                    string relativePath = Path.GetRelativePath(rootDirectory, file);
                    builder.AddFile(relativePath, file);
                }
                foreach (var dir in Directory.GetDirectories(currentDirectory))
                {
                    BuildDirectory(builder, rootDirectory, dir);
                }
            }
        }
    }

    public static class Patcher
    {
        public const int IdentifierOffset = 32768;
        public const int ApplicationIdentifierOffset = IdentifierOffset + 883;
        public const int ApplicationIdentifierLength = 128;
        public const int VolumeSetIdentifierOffset = IdentifierOffset + 574;
        public const int VolumeSetIdentifierLength = 128;
        public const int DataPreparerIdentifierOffset = IdentifierOffset + 702;
        public const int DataPreparerIdentifierLength = 128;

        public static void PatchApplicationIdentifier(string isoPath, string newVolumeIdentifier)
        {
            using (var fs = new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite))
            {
                PatchField(fs, ApplicationIdentifierOffset, ApplicationIdentifierLength, newVolumeIdentifier);
            }
        }
        public static void PatchVolumeSetName(string isoPath, string newApplicationIdentifier)
        {
            using (var fs = new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite))
            {
                PatchField(fs, VolumeSetIdentifierOffset, VolumeSetIdentifierLength, newApplicationIdentifier);
            }
        }
        public static void PatchDataPreparerIdentifier(string isoPath, string newDataPreparerIdentifier)
        {
            using (var fs = new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite))
            {
                PatchField(fs, DataPreparerIdentifierOffset, DataPreparerIdentifierLength, newDataPreparerIdentifier);
            }
        }
        public static void PatchIdentifiers(string isoPath, Identifiers identifiers)
        {
            using (var fs = new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite))
            {
                if (identifiers.ApplicationIdentifier != null)
                    PatchField(fs, ApplicationIdentifierOffset, ApplicationIdentifierLength, identifiers.ApplicationIdentifier);
                if (identifiers.VolumeSetIdentifier != null)
                    PatchField(fs, VolumeSetIdentifierOffset, VolumeSetIdentifierLength, identifiers.VolumeSetIdentifier);
                if (identifiers.DataPreparerIdentifier != null)
                    PatchField(fs, DataPreparerIdentifierOffset, DataPreparerIdentifierLength, identifiers.DataPreparerIdentifier);
            }
        }
        public static void PatchField(FileStream fs, long offset, int length, string value)
        {
            fs.Seek(offset, SeekOrigin.Begin);
            var bytes = new byte[length];
            var truncated = value.Length > length ? value[..length] : value.PadRight(length);
            Encoding.ASCII.GetBytes(truncated).CopyTo(bytes, 0);
            fs.Write(bytes, 0, length);
        }
    }
    public class Identifiers
    {
        public string? ApplicationIdentifier { get; set; }
        public string? VolumeIdentifier { get; set; }
        public string? VolumeSetIdentifier { get; set; }
        public string? DataPreparerIdentifier { get; set; }
    }
}