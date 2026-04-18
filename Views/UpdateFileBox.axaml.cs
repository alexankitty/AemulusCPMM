using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Octokit;

namespace AemulusModManager.Avalonia.Views;

public partial class UpdateFileBox : Window
{
    public string? ChosenFileUrl { get; set; }
    public string? ChosenFileName { get; set; }
    public string Host { get; set; } = "";

    public UpdateFileBox()
    {
        InitializeComponent();
    }

    // GameBanana Files
    public UpdateFileBox(List<GameBananaItemFile> files, string packageName)
    {
        InitializeComponent();
        FileList.ItemsSource = files;
        TitleBox.Text = packageName;
        Host = "gamebanana";
    }

    // GitHub Files
    public UpdateFileBox(IReadOnlyList<ReleaseAsset> files, string packageName)
    {
        InitializeComponent();
        TitleBox.Text = packageName;
        var convList = new List<GithubFile>();
        foreach (var file in files)
        {
            convList.Add(new GithubFile
            {
                FileName = file.Name,
                Downloads = file.DownloadCount,
                Filesize = file.Size,
                Description = file.Label,
                DateAdded = file.UpdatedAt.DateTime,
                DownloadUrl = file.BrowserDownloadUrl
            });
        }
        FileList.ItemsSource = convList;
        Host = "github";
    }

    private void SelectButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            if (Host == "gamebanana" && button.DataContext is GameBananaItemFile gbFile)
            {
                ChosenFileUrl = gbFile.DownloadUrl;
                ChosenFileName = gbFile.FileName;
            }
            else if (Host == "github" && button.DataContext is GithubFile ghFile)
            {
                ChosenFileUrl = ghFile.DownloadUrl;
                ChosenFileName = ghFile.FileName;
            }
        }
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
