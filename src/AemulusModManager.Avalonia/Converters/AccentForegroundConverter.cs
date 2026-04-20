using System;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace AemulusModManager.Avalonia.Converters
{
    public class AccentForegroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            Color color;
            if (value is ISolidColorBrush brush)
            {
                color = brush.Color;
            }
            else if (value is Color c)
            {
                color = c;
            }
            else if (value is string s && Color.TryParse(s, out var parsed))
            {
                color = parsed;
            }
            else
            {
                // fallback: white
                return Brushes.White;
            }

            // Perceived brightness formula
            double brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255;
            return brightness > 0.6 ? Brushes.Black : Brushes.White;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
