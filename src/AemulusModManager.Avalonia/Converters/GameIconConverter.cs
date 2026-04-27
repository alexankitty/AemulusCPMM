using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace AemulusModManager.Avalonia.Converters;

public class GameIconConverter : IValueConverter {
    private static readonly Dictionary<string, string> GameIconMap = new() {
        ["Persona 1 (PSP)"] = "avares://AemulusPackageManager/Assets/p1pspicon.png",
        ["Persona 3 FES"] = "avares://AemulusPackageManager/Assets/p3ficon.png",
        ["Persona 3 Portable"] = "avares://AemulusPackageManager/Assets/p3picon.png",
        ["Persona 4 Golden"] = "avares://AemulusPackageManager/Assets/p4gicon.png",
        ["Persona 4 Golden (Vita)"] = "avares://AemulusPackageManager/Assets/p4gicon.png",
        ["Persona 5"] = "avares://AemulusPackageManager/Assets/p5icon.png",
        ["Persona 5 Royal (PS4)"] = "avares://AemulusPackageManager/Assets/p5ricon.png",
        ["Persona 5 Royal (Switch)"] = "avares://AemulusPackageManager/Assets/p5ricon.png",
        ["Persona 5 Strikers"] = "avares://AemulusPackageManager/Assets/p5sicon.png",
        ["Persona Q"] = "avares://AemulusPackageManager/Assets/pqicon.png",
        ["Persona Q2"] = "avares://AemulusPackageManager/Assets/pq2icon.png",
    };

    private static readonly Dictionary<string, Bitmap?> _cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is string gameName && GameIconMap.TryGetValue(gameName, out var uri)) {
            if (_cache.TryGetValue(gameName, out var cached))
                return cached;

            try {
                var assetUri = new Uri(uri);
                using var stream = global::Avalonia.Platform.AssetLoader.Open(assetUri);
                var bitmap = new Bitmap(stream);
                _cache[gameName] = bitmap;
                return bitmap;
            }
            catch {
                _cache[gameName] = null;
                return null;
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
