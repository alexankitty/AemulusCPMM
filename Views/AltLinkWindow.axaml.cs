using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AemulusModManager.Avalonia.Views;

public partial class AltLinkWindow : Window
{
    public GameBananaAlternateFileSource? ChosenSource { get; set; }

    public AltLinkWindow()
    {
        InitializeComponent();
    }

    public AltLinkWindow(List<GameBananaAlternateFileSource> sources)
    {
        InitializeComponent();
        LinkList.ItemsSource = sources;
        if (sources.Count > 0)
            LinkList.SelectedIndex = 0;
    }

    private void SelectButton_Click(object? sender, RoutedEventArgs e)
    {
        ChosenSource = LinkList.SelectedItem as GameBananaAlternateFileSource;
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
