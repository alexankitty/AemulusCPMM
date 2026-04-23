using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AemulusModManager.Avalonia.Converters
{
    public class StringNotEmptyToVisibleConverter : IValueConverter
    {
        public static readonly StringNotEmptyToVisibleConverter Instance = new StringNotEmptyToVisibleConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var str = value as string;
            return !string.IsNullOrWhiteSpace(str) || !string.IsNullOrEmpty(str);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
