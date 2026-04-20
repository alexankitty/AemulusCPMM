using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using AemulusModManager.Avalonia.ViewModels;

namespace AemulusModManager.Avalonia.Views;

public partial class ConfigWindow : Window
{
    public event Func<System.Threading.Tasks.Task>? UnpackRequested;

    public ConfigWindow()
    {
        InitializeComponent();
    }

    public ConfigWindow(ConfigWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        // Set AccentButtonBackground resource based on GameTitle
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
        // Set foreground to either black or white based on brightness
        double brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255;
        var foreground = brightness > 0.6 ? Brushes.Black : Brushes.White;
        this.Resources["AccentButtonForeground"] = foreground;
        this.Resources["AccentButtonForegroundPressed"] = foreground;
        this.Resources["AccentButtonForegroundPointerOver"] = foreground;
        this.Resources["AccentButtonForegroundDisabled"] = foreground;
    }

    private async void BrowseOutputClick(object? sender, RoutedEventArgs e)
    {
        var result = await BrowseFolder("Select Output Folder");
        if (result != null && DataContext is ConfigWindowViewModel vm)
            vm.OutputFolder = result;
    }

    private async void BrowseExeClick(object? sender, RoutedEventArgs e)
    {
        var result = await BrowseFile("Select File");
        if (result != null && DataContext is ConfigWindowViewModel vm)
            vm.ExePath = result;
    }

    private async void BrowseElfClick(object? sender, RoutedEventArgs e)
    {
        var result = await BrowseFile("Select ELF File");
        if (result != null && DataContext is ConfigWindowViewModel vm)
            vm.ElfPath = result;
    }

    private async void BrowseLauncherClick(object? sender, RoutedEventArgs e)
    {
        var result = await BrowseFile("Select Launcher/Emulator");
        if (result != null && DataContext is ConfigWindowViewModel vm)
            vm.LauncherPath = result;
    }

    private async void BrowseCheatsClick(object? sender, RoutedEventArgs e)
    {
        var result = await BrowseFolder("Select Cheats Folder");
        if (result != null && DataContext is ConfigWindowViewModel vm)
            vm.CheatsPath = result;
    }

    private async void BrowseCheatsWSClick(object? sender, RoutedEventArgs e)
    {
        var result = await BrowseFolder("Select Cheats WS Folder");
        if (result != null && DataContext is ConfigWindowViewModel vm)
            vm.CheatsWSPath = result;
    }

    private async void BrowseTexturesClick(object? sender, RoutedEventArgs e)
    {
        var result = await BrowseFolder("Select Textures Folder");
        if (result != null && DataContext is ConfigWindowViewModel vm)
            vm.TexturesPath = result;
    }

    private async void UnpackClick(object? sender, RoutedEventArgs e)
    {
        if (UnpackRequested != null)
            await UnpackRequested.Invoke();
    }

    private async System.Threading.Tasks.Task<string?> BrowseFolder(string title)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return null;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    private async System.Threading.Tasks.Task<string?> BrowseFile(string title)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return null;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
