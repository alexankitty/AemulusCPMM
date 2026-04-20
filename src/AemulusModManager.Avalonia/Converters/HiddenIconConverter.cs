using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AemulusModManager.Avalonia.Converters;

public class HiddenIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "fa-solid fa-eye" : "fa-solid fa-eye-slash";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
