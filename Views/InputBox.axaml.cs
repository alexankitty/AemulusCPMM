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

    public InputBox(string prompt, bool showCopyCheckbox = true) : this()
    {
        PromptText.Text = prompt;
        CopyCheckBox.IsVisible = showCopyCheckbox;
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        var name = InputTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
            return;
        Result = name;
        CopyLoadout = CopyCheckBox.IsChecked == true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}
