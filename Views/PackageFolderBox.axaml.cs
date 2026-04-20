using Avalonia.Controls;
using Avalonia.Interactivity;
using AemulusModManager.Avalonia.ViewModels;

namespace AemulusModManager.Avalonia.Views;

public partial class PackageFolderBox : Window
{
    public string? ChosenFolder { get; set; }

    public PackageFolderBox()
    {
        InitializeComponent();
    }

    public PackageFolderBox(DialogWindowViewModel dialogVm, string[] folders, string packageName) : this()
    {
        DataContext = dialogVm;
        foreach(var prop in dialogVm.AccentProps)
        {
            this.Resources[prop] = dialogVm.GetType().GetProperty(prop)?.GetValue(dialogVm);
        }
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
