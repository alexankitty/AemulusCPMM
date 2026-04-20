using System;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using AemulusModManager.Avalonia.ViewModels;
using AemulusModManager.Utilities.PackageUpdating;

namespace AemulusModManager.Avalonia.Views;

public partial class DownloadWindow : Window
{
    public bool YesNo { get; set; }

    public DownloadWindow()
    {
        InitializeComponent();
    }

    public DownloadWindow(DialogWindowViewModel dialogVm, string name, string author, Uri? image = null)
    {
        InitializeComponent();
        DataContext = dialogVm;
        foreach(var prop in dialogVm.AccentProps)
        {
            this.Resources[prop] = dialogVm.GetType().GetProperty(prop)?.GetValue(dialogVm);
        }
        DownloadText.Text = $"{name}\nSubmitted by {author}";
        if (image != null)
            _ = LoadImageAsync(image);
    }

    public DownloadWindow(DialogWindowViewModel dialogVm, GameBananaRecord record)
    {
        InitializeComponent();
        DataContext = dialogVm;
        foreach(var prop in dialogVm.AccentProps)
        {
            this.Resources[prop] = dialogVm.GetType().GetProperty(prop)?.GetValue(dialogVm);
        }
        DownloadText.Text = $"{record.Title}\nSubmitted by {record.Owner?.Name}";
        if (record.Image != null)
            _ = LoadImageAsync(record.Image);
    }

    public DownloadWindow(DialogWindowViewModel dialogVm, GameBananaAPIV4 response)
    {
        InitializeComponent();
        DataContext = dialogVm;
        foreach(var prop in dialogVm.AccentProps)
        {
            this.Resources[prop] = dialogVm.GetType().GetProperty(prop)?.GetValue(dialogVm);
        }
        DownloadText.Text = $"{response.Title}\nSubmitted by {response.Owner?.Name}";
        if (response.Image != null)
            _ = LoadImageAsync(response.Image);
    }

    private async Task LoadImageAsync(Uri uri)
    {
        try
        {
            using var client = new HttpClient();
            var data = await client.GetByteArrayAsync(uri);
            using var stream = new System.IO.MemoryStream(data);
            Preview.Source = new Bitmap(stream);
        }
        catch
        {
            // Ignore image load failures
        }
    }

    private void Yes_Click(object? sender, RoutedEventArgs e)
    {
        YesNo = true;
        Close();
    }

    private void No_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
