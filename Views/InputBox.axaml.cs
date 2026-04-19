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

    public InputBox(DialogWindowViewModel viewModel, string prompt) : this()
    {
        DataContext = viewModel;
        PromptText.Text = prompt;
        if (viewModel is not null)
        {
            SetAccentButtonBackground(viewModel.GameTitle);
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.GameTitle))
                {
                    SetAccentButtonBackground(viewModel.GameTitle);
                }
            };
        }
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
