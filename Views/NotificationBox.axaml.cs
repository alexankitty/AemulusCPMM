using AemulusModManager.Avalonia.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace AemulusModManager.Avalonia.Views;

public partial class NotificationBox : Window
{
    public bool YesNo { get; set; }

    public NotificationBox()
    {
        InitializeComponent();
    }

    public NotificationBox(string message, bool ok = true)
    {
        InitializeComponent();
        Notification.Text = message;
        if (ok)
        {
            OkButton.IsVisible = true;
        }
        else
        {
            YesButton.IsVisible = true;
            NoButton.IsVisible = true;
        }
        if (message.Length > 40)
            Notification.TextAlignment = global::Avalonia.Media.TextAlignment.Left;
    }

    private void SetAccentButtonBackground(string gameTitle)
    {
        var brush = AemulusModManager.Avalonia.Converters.GameColorConverter.GetBrush(gameTitle);
        this.Resources["AccentButtonBackground"] = brush;

        // Use AccentDarkenConverter logic for darkening
        var color = ((SolidColorBrush)brush).Color;
        this.Resources["AccentButtonBackgroundPressed"] = new SolidColorBrush(Utilities.Colors.Darken(color, 0.2));
        this.Resources["AccentButtonBackgroundPointerOver"] = new SolidColorBrush(Utilities.Colors.Darken(color, 0.3));
        this.Resources["AccentButtonBackgroundDisabled"] = new SolidColorBrush(Utilities.Colors.Darken(color, 0.7));
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Yes_Button_Click(object? sender, RoutedEventArgs e)
    {
        YesNo = true;
        Close();
    }
}
