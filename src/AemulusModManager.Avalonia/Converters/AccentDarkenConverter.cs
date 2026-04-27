using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace AemulusModManager.Avalonia.Converters {
    public class AccentDarkenConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is Color color && parameter is string paramStr && double.TryParse(paramStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount)) {
                return new SolidColorBrush(Utilities.Colors.Darken(color, amount));
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
