using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AemulusModManager.Avalonia.Converters;

public class ShowHiddenConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (values.Count >= 2 && values[0] is bool hidden && values[1] is bool showingHidden)
            {
                // If the row isn't hidden or hidden packages are being shown
                return !hidden || showingHidden ? false : true;
            }
        }
        catch
        {
            // Fall through
        }
        return false;
    }
}
