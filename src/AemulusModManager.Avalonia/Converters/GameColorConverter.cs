using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AemulusModManager.Avalonia.Converters;

public class GameColorConverter : IValueConverter {
    public static readonly Dictionary<string, string> GameColors = new() {
        ["Persona 1 (PSP)"] = "#B683FC",
        ["Persona 3 FES"] = "#6EB0F7",
        ["Persona 3 Portable"] = "#FC83E3",
        ["Persona 4 Golden"] = "#F5E63D",
        ["Persona 4 Golden (Vita)"] = "#F5A83D",
        ["Persona 5"] = "#FB5151",
        ["Persona 5 Royal (PS4)"] = "#F76484",
        ["Persona 5 Royal (Switch)"] = "#F76484",
        ["Persona 5 Strikers"] = "#25F4B8",
        ["Persona Q"] = "#9000FD",
        ["Persona Q2"] = "#FB846A",
    };

    public static readonly Dictionary<string, string> GameHoverColors = new() {
        ["Persona 1 (PSP)"] = "#5B417E",
        ["Persona 3 FES"] = "#37587B",
        ["Persona 3 Portable"] = "#7E4171",
        ["Persona 4 Golden"] = "#7A731E",
        ["Persona 4 Golden (Vita)"] = "#7A541E",
        ["Persona 5"] = "#7D2828",
        ["Persona 5 Royal (PS4)"] = "#7B3242",
        ["Persona 5 Royal (Switch)"] = "#7B3242",
        ["Persona 5 Strikers"] = "#127A5C",
        ["Persona Q"] = "#560052",
        ["Persona Q2"] = "#7D4235",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is string gameName && GameColors.TryGetValue(gameName, out var hex))
            return SolidColorBrush.Parse(hex);
        return SolidColorBrush.Parse("#F5E63D");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    public static IBrush GetBrush(string gameName) {
        if (GameColors.TryGetValue(gameName, out var hex))
            return SolidColorBrush.Parse(hex);
        return SolidColorBrush.Parse("#F5E63D");
    }

    public static string GetHex(string gameName) {
        if (GameColors.TryGetValue(gameName, out var hex))
            return hex;
        return "#F5E63D";
    }

    public static IBrush GetHoverBrush(string gameName) {
        if (GameHoverColors.TryGetValue(gameName, out var hex))
            return SolidColorBrush.Parse(hex);
        return SolidColorBrush.Parse("#7A731E");
    }
}
