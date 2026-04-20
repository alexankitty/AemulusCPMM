using AemulusModManager.Avalonia.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace AemulusModManager.Avalonia.Views;

public partial class InputBox : Window
{
    public string? Result { get; private set; }
    public bool CopyLoadout { get; private set; }

    public InputBox()
    {
        InitializeComponent();
    }

    public InputBox(DialogWindowViewModel dialogVm, string prompt) : this()
    {
        DataContext = dialogVm;
        PromptText.Text = prompt;
        foreach(var prop in dialogVm.AccentProps)
        {
            this.Resources[prop] = dialogVm.GetType().GetProperty(prop)?.GetValue(dialogVm);
        }
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
