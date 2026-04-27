using System.Collections.Generic;
using AemulusModManager.Avalonia.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AemulusModManager.Avalonia.Views;

public partial class AltLinkWindow : Window {
    public GameBananaAlternateFileSource? ChosenSource { get; set; }

    public AltLinkWindow() {
        InitializeComponent();
    }

    public AltLinkWindow(DialogWindowViewModel dialogVm, List<GameBananaAlternateFileSource> sources) {
        InitializeComponent();
        DataContext = dialogVm;
        foreach (var prop in dialogVm.AccentProps) {
            this.Resources[prop] = dialogVm.GetType().GetProperty(prop)?.GetValue(dialogVm);
        }
        LinkList.ItemsSource = sources;
        if (sources.Count > 0)
            LinkList.SelectedIndex = 0;
    }

    private void SelectButton_Click(object? sender, RoutedEventArgs e) {
        ChosenSource = LinkList.SelectedItem as GameBananaAlternateFileSource;
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e) {
        Close();
    }
}
