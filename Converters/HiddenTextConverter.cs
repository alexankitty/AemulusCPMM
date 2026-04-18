using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AemulusModManager.Avalonia.Converters;

public class HiddenTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true
            ? "Hide All Hidden Packages\n(Ctrl+H)"
            : "Show All Hidden Packages\n(Ctrl+H)";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
