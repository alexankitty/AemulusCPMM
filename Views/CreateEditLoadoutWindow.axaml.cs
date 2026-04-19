using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AemulusModManager.Utilities;

namespace AemulusModManager.Avalonia.Views;

public partial class CreateEditLoadoutWindow : Window
{
    private readonly string _game;
    public string LoadoutName { get; set; } = "";
    public bool DeleteLoadout { get; set; }
    public bool CopyCurrentLoadout => CopyLoadout?.IsChecked == true;
    private readonly string? _originalName;
    private readonly bool _editing;

    public CreateEditLoadoutWindow()
    {
        InitializeComponent();
    }

    public CreateEditLoadoutWindow(string game, string? currentName = null, bool editing = false)
    {
        _game = game;
        _originalName = currentName;
        _editing = editing;
        InitializeComponent();
        if (currentName != null)
        {
            Title = $"Edit {currentName} loadout";
            LoadoutName = currentName;
            NameBox.Text = currentName;
            CopyLoadout.IsVisible = false;
            Height = 120;
        }
        if (currentName == null || !editing)
        {
            DeleteButton.IsVisible = false;
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
    }

    private async void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        if(_originalName != null && _originalName == NameBox.Text) Close();
        if (NameBox.Text == "Add new loadout")
        {
            ParallelLogger.Log("[ERROR] Invalid loadout name, try another one.");
            var notification = new NotificationBox("Invalid loadout name, try another one.");
            await notification.ShowDialog(this);
        }
        else if (_originalName != NameBox.Text && _editing)
        {
            string configPath = Path.Combine(
                Utilities.AppPaths.ConfigDir,
                _game, $"{_originalName}.xml");
            if (File.Exists(configPath))            {
                string newConfigPath = Path.Combine(
                    Utilities.AppPaths.ConfigDir,
                    _game, $"{NameBox.Text}.xml");
                File.Move(configPath, newConfigPath);
            }
            Close();
        }
        else
        {
            string configPath = Path.Combine(
                Utilities.AppPaths.ConfigDir,
                _game, $"{NameBox.Text}.xml");
            if (!File.Exists(configPath))
            {
                Close();
            }
            else
            {
                ParallelLogger.Log($"[ERROR] Loadout name {NameBox.Text} already exists, try another one.");
                var notification = new NotificationBox($"Loadout name {NameBox.Text} already exists, try another one.");
                await notification.ShowDialog(this);
            }
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
