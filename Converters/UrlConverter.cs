using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AemulusModManager.Avalonia.Converters;

public class UrlConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return ConvertUrl(value?.ToString());
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public static string? ConvertUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        if ((Uri.TryCreate(url, UriKind.Absolute, out var uri) || Uri.TryCreate("http://" + url, UriKind.Absolute, out uri)) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            string host = uri.DnsSafeHost;
            return host switch
            {
                "www.gamebanana.com" or "gamebanana.com" => "GameBanana",
                "nexusmods.com" or "www.nexusmods.com" => "Nexus",
                "www.shrinefox.com" or "shrinefox.com" => "ShrineFox",
                "www.github.com" or "github.com" => "GitHub",
                _ => "Other",
            };
        }
        return null;
    }
}
