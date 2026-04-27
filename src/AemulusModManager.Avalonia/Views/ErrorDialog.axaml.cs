using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;


namespace AemulusModManager.Avalonia.Views;

public partial class ErrorDialog : Window {
    private readonly CancellationTokenSource _cts;

    public ErrorDialog(string message, CancellationTokenSource cts) {
        InitializeComponent();
        this.FindControl<TextBlock>("ErrorText").Text = message;
        _cts = cts;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) {
        Close();
        _cts.Cancel();
    }
}
