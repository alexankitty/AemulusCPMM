using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AemulusModManager.Avalonia.Views;

public partial class InputBox : Window
{
    public string? Result { get; private set; }
    public bool CopyLoadout { get; private set; }

    public InputBox()
    {
        InitializeComponent();
    }

    public InputBox(string prompt) : this()
    {
        PromptText.Text = prompt;
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        var name = InputTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
            return;
        Result = name;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}
