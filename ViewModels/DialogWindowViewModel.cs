using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using System;

namespace AemulusModManager.Avalonia.ViewModels;

public partial class DialogWindowViewModel : ObservableObject
{
    [ObservableProperty] public string _gameTitle = "";
    [ObservableProperty] public SolidColorBrush? _accentButtonBackground;
    [ObservableProperty] public SolidColorBrush? _accentButtonForeground;
    [ObservableProperty] public SolidColorBrush? _accentButtonForegroundPressed;
    [ObservableProperty] public SolidColorBrush? _accentButtonForegroundPointerOver;
    [ObservableProperty] public SolidColorBrush? _accentButtonForegroundDisabled;
    [ObservableProperty] public SolidColorBrush? _accentButtonBackgroundPressed;
    [ObservableProperty] public SolidColorBrush? _accentButtonBackgroundPointerOver;
    [ObservableProperty] public SolidColorBrush? _accentButtonBackgroundDisabled;
    public string[] AccentProps => ["AccentButtonBackground", "AccentButtonForeground", "AccentButtonForegroundPressed", "AccentButtonForegroundPointerOver", "AccentButtonForegroundDisabled", "AccentButtonBackgroundPressed", "AccentButtonBackgroundPointerOver", "AccentButtonBackgroundDisabled"];

    public DialogWindowViewModel(string gameTitle)
    {
        GameTitle = gameTitle;
        UpdateAccentColors();
    }
    public void UpdateAccentColors(string gameTitle)
    {
        GameTitle = gameTitle;
        UpdateAccentColors();
    }
    
    private void UpdateAccentColors()
    {
        Console.WriteLine("Updating accent colors for game: " + GameTitle);
        if(string.IsNullOrWhiteSpace(GameTitle))
            return;
        var hex = Converters.GameColorConverter.GetHex(GameTitle);
        var brush = SolidColorBrush.Parse(hex);
        AccentButtonBackground = brush;
        var color = brush.Color;
        AccentButtonBackgroundPressed = new SolidColorBrush(Utilities.Colors.Darken(color, 0.2));
        AccentButtonBackgroundPointerOver = new SolidColorBrush(Utilities.Colors.Darken(color, 0.3));
        AccentButtonBackgroundDisabled = new SolidColorBrush(Utilities.Colors.Darken(color, 0.7));
        // Set foreground to either black or white based on brightness
        double brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255;
        AccentButtonForeground = brightness > 0.6 ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.White);
        AccentButtonForegroundPressed = AccentButtonForeground;
        AccentButtonForegroundPointerOver = AccentButtonForeground;
        AccentButtonForegroundDisabled = AccentButtonForeground;
    }
}