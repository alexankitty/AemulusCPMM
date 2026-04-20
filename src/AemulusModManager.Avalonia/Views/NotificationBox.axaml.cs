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

    public NotificationBox(DialogWindowViewModel dialogVm, string message, bool ok = true)
    {
        InitializeComponent();
        DataContext = dialogVm;
        foreach(var prop in dialogVm.AccentProps)
        {
            this.Resources[prop] = dialogVm.GetType().GetProperty(prop)?.GetValue(dialogVm);
        }
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
