using System;
using System.IO;
using System.Xml.Serialization;
using AemulusModManager.Avalonia.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AemulusModManager.Avalonia.Views;

public partial class ChangelogBox : Window {
    public bool YesNo { get; set; }
    private DisplayedMetadata? _row;
    private string? _version;
    private string? _path;

    public ChangelogBox() {
        InitializeComponent();
    }

    public ChangelogBox(DialogWindowViewModel dialogVm, GameBananaItemUpdate update, string packageName, string text,
        DisplayedMetadata row, string version, string path, bool ok = true) {
        _row = row;
        _version = version;
        _path = path;
        InitializeComponent();
        DataContext = dialogVm;
        foreach (var prop in dialogVm.AccentProps) {
            this.Resources[prop] = dialogVm.GetType().GetProperty(prop)?.GetValue(dialogVm);
        }
        ChangesGrid.ItemsSource = update.Changes;
        Title = $"{packageName} Changelog";
        VersionLabel.Text = update.Title;
        if (update.Version != null)
            VersionLabel.Text += $" ({update.Version})";
        Text.Text = text;
        if (ok) {
            OkButton.IsVisible = true;
        }
        else {
            YesButton.IsVisible = true;
            NoButton.IsVisible = true;
            SkipButton.IsVisible = true;
        }
    }

    public ChangelogBox(DialogWindowViewModel dialogVm, GameBananaItemUpdate update, string packageName, string text, bool ok = true) {
        InitializeComponent();
        DataContext = dialogVm;
        foreach (var prop in dialogVm.AccentProps) {
            this.Resources[prop] = dialogVm.GetType().GetProperty(prop)?.GetValue(dialogVm);
        }
        ChangesGrid.ItemsSource = update.Changes;
        Title = $"{packageName} Changelog";
        VersionLabel.Text = update.Title;
        if (update.Version != null)
            VersionLabel.Text += $" ({update.Version})";
        Text.Text = text;
        if (ok) {
            OkButton.IsVisible = true;
        }
        else {
            YesButton.IsVisible = true;
            NoButton.IsVisible = true;
        }
    }

    private void Button_Click(object? sender, RoutedEventArgs e) {
        Close();
    }

    private void Skip_Button_Click(object? sender, RoutedEventArgs e) {
        if (_row != null && _path != null) {
            var m = new Metadata {
                name = _row.name,
                author = _row.author,
                id = _row.id,
                version = _row.version,
                link = _row.link,
                description = _row.description,
                skippedVersion = _version
            };
            try {
                using var streamWriter = File.Create(_path);
                try {
                    var xsp = new XmlSerializer(typeof(Metadata));
                    xsp.Serialize(streamWriter, m);
                }
                catch (Exception ex) {
                    AemulusModManager.Utilities.ParallelLogger.Log($@"[ERROR] Couldn't serialize {_path} ({ex.Message})");
                }
            }
            catch (Exception ex) {
                AemulusModManager.Utilities.ParallelLogger.Log($"[ERROR] Error trying to set skipped version for {_row.name}: {ex.Message}");
            }
        }
        Close();
    }

    private void Yes_Button_Click(object? sender, RoutedEventArgs e) {
        YesNo = true;
        Close();
    }
}
