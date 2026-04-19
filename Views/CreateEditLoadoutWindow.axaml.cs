using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AemulusModManager.Utilities;
using Avalonia.Media;
using System;
using AemulusModManager.Avalonia.ViewModels;

namespace AemulusModManager.Avalonia.Views;

public partial class CreateEditLoadoutWindow : Window
{
    private readonly string _game;
    public string LoadoutName { get; set; } = "";
    public bool DeleteLoadout { get; set; }
    public bool CopyCurrentLoadout => CopyLoadout.IsChecked ?? false;
    private readonly string? _originalName;
    private readonly bool _editing;
    public string? ResultName { get; private set; }

    public CreateEditLoadoutWindow()
    {
        InitializeComponent();
    }

    public CreateEditLoadoutWindow(DialogWindowViewModel viewModel, string game, string? currentName = null, bool editing = false)
    {
        DataContext = viewModel;
        Console.WriteLine("Opened window");
        _game = game;
        _originalName = currentName;
        _editing = editing;
        InitializeComponent();
        if (currentName != null && editing)
        {
            Title = $"Edit {currentName} loadout";
            LoadoutName = currentName;
            NameBox.Text = currentName;
            Height = 120;
        }
        if (currentName == null || !editing)
        {
            DeleteButton.IsVisible = false;
            CopyLoadout.IsVisible = true;
        }
        else
        {
            DeleteButton.IsVisible = true;
            CopyLoadout.IsVisible = false;
            CreateButton.Content = "Save";
        }

        NameBox.TextChanged += (_, _) =>
        {
            LoadoutName = NameBox.Text ?? "";
            CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(NameBox.Text);
        };

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
    }

    private async void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine($"Copy current loadout: {CopyCurrentLoadout}");
        if (string.IsNullOrWhiteSpace(NameBox.Text) ||
           _originalName == NameBox.Text)
        {
            Close();
            return;
        }
        string configPath = Path.Combine(
                Utilities.AppPaths.ConfigDir,
                _game, $"{NameBox.Text}.xml");
        //validation
        if (NameBox.Text == "Add new loadout")
        {
            ParallelLogger.Log("[ERROR] Invalid loadout name, try another one.");
            var notification = new NotificationBox("Invalid loadout name, try another one.");
            await notification.ShowDialog(this);
        }
        //check if name already exists
        else if (File.Exists(configPath))
        {
            ParallelLogger.Log($"[ERROR] Loadout name {NameBox.Text} already exists, try another one.");
            var notification = new NotificationBox($"Loadout name {NameBox.Text} already exists, try another one.");
            await notification.ShowDialog(this);
        }
        //editing but name is changed, rename file
        else if (_originalName != NameBox.Text && _editing)
        {
            configPath = Path.Combine(
                Utilities.AppPaths.ConfigDir,
                _game, $"{_originalName}.xml");
            if (File.Exists(configPath))            {
                string newConfigPath = Path.Combine(
                    Utilities.AppPaths.ConfigDir,
                    _game, $"{NameBox.Text}.xml");
                File.Move(configPath, newConfigPath);
            }
            ResultName = NameBox.Text;
            Close();
        }
        //Create new loadout
        else
        {
            if(CopyCurrentLoadout == true && _originalName != null)
            {
                string copyPath = Path.Combine(
                Utilities.AppPaths.ConfigDir,
                _game, $"{_originalName}.xml");
                if(File.Exists(copyPath))
                {
                    File.Copy(copyPath, configPath);
                }
                ResultName = NameBox.Text;
                Close();
            }
            ResultName = NameBox.Text;
            Close();
        }
        
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        LoadoutName = "";
        Close();
    }

    private async void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        string configPath = Utilities.AppPaths.ConfigDir;
        string[] loadoutFiles = Directory.GetFiles(Path.Combine(configPath, _game))
            .Where(path => Path.GetExtension(path) == ".xml")
            .ToArray();

        if (loadoutFiles.Length == 1)
        {
            var notification = new NotificationBox("You cannot delete the last loadout");
            ParallelLogger.Log("[ERROR] You cannot delete the last loadout");
            await notification.ShowDialog(this);
        }
        else
        {
            var notification = new NotificationBox(
                $"Are you sure you want to delete {_originalName} loadout?\nThis cannot be undone.", false);
            await notification.ShowDialog(this);
            if (notification.YesNo)
            {
                configPath = Path.Combine(
                Utilities.AppPaths.ConfigDir,
                _game, $"{NameBox.Text}.xml");
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
                Close();
            }
        }
    }
}
