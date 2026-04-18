using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AemulusModManager.Avalonia.Views;

public partial class PackageFolderBox : Window
{
    public string? ChosenFolder { get; set; }

    public PackageFolderBox()
    {
        InitializeComponent();
    }

    public PackageFolderBox(string[] folders, string packageName)
    {
        InitializeComponent();
        FileGrid.ItemsSource = folders;
        FileGrid.SelectedIndex = 0;
        Title = $"Aemulus Package Manager - {packageName}";
    }

    private void SelectButton_Click(object? sender, RoutedEventArgs e)
    {
        ChosenFolder = FileGrid.SelectedItem as string;
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
