using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace AemulusModManager.Avalonia.Converters
{
    public class AccentDarkenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color && parameter is string paramStr && double.TryParse(paramStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount))
            {
                return new SolidColorBrush(Darken(color, amount));
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private Color Darken(Color color, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);
            return Color.FromArgb(
                color.A,
                (byte)(color.R * (1 - amount)),
                (byte)(color.G * (1 - amount)),
                (byte)(color.B * (1 - amount))
            );
        }
    }
}
