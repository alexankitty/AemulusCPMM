using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AemulusModManager.Utilities;

namespace AemulusModManager.Avalonia.Views;

public partial class CreatePackageWindow : Window
{
    public Metadata? ResultMetadata { get; set; }
    public string? ThumbnailPath { get; set; }
    private bool _edited;
    private bool _editing;
    private string? _skippedVersion = "";

    public CreatePackageWindow()
    {
        InitializeComponent();
        SetupTextChangedHandlers();
    }

    public CreatePackageWindow(Metadata? m)
    {
        InitializeComponent();
        SetupTextChangedHandlers();
        if (m != null)
        {
            Title = $"Edit {m.name}";
            NameBox.Text = m.name;
            AuthorBox.Text = m.author;
            IDBox.Text = m.id;
            VersionBox.Text = m.version;
            LinkBox.Text = m.link;
            DescBox.Text = m.description;
            _skippedVersion = m.skippedVersion;
            _editing = true;
            UpdateAllowUpdates();
            if (IDBox.Text != AuthorBox.Text?.Replace(" ", "").ToLower() + "."
                    + NameBox.Text?.Replace(" ", "").ToLower() && IDBox.Text?.Length > 0)
                _edited = true;
            if (IDBox.Text == NameBox.Text?.Replace(" ", "").ToLower())
                _edited = false;
        }
    }

    private void SetupTextChangedHandlers()
    {
        NameBox.TextChanged += (_, _) => OnNameOrAuthorChanged();
        AuthorBox.TextChanged += (_, _) => OnNameOrAuthorChanged();
        LinkBox.TextChanged += (_, _) => UpdateAllowUpdates();
        IDBox.GotFocus += (_, _) => { if (!_edited) _edited = true; };
    }

    private void OnNameOrAuthorChanged()
    {
        CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(NameBox.Text);
        if (string.IsNullOrEmpty(NameBox.Text) && string.IsNullOrEmpty(AuthorBox.Text) && string.IsNullOrEmpty(IDBox.Text))
            _edited = false;
        if (!_edited && !_editing)
        {
            var name = NameBox.Text?.Replace(" ", "").ToLower() ?? "";
            var author = AuthorBox.Text?.Replace(" ", "").ToLower() ?? "";
            IDBox.Text = !string.IsNullOrEmpty(author) && !string.IsNullOrEmpty(name)
                ? $"{author}.{name}"
                : !string.IsNullOrEmpty(name) ? name : author;
        }
    }

    private void UpdateAllowUpdates()
    {
        bool updatable = PackageUpdatable();
        AllowUpdates.IsEnabled = updatable;
        if (!updatable) AllowUpdates.IsChecked = false;
        else if (_skippedVersion == "all") AllowUpdates.IsChecked = false;
        else AllowUpdates.IsChecked = true;
    }

    private void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        var metadata = new Metadata();
        string dirName = !string.IsNullOrEmpty(VersionBox.Text)
            ? $"{NameBox.Text} {VersionBox.Text}" : NameBox.Text ?? "";
        dirName = $"Packages{Path.DirectorySeparatorChar}{string.Join("_", dirName.Split(Path.GetInvalidFileNameChars()))}";

        if (!Directory.Exists(dirName) || _editing)
        {
            metadata.skippedVersion = AllowUpdates.IsChecked != true ? "all" : null;
            metadata.name = NameBox.Text ?? "";
            metadata.author = AuthorBox.Text ?? "";
            metadata.version = VersionBox.Text ?? "";
            metadata.id = IDBox.Text ?? "";
            metadata.link = LinkBox.Text ?? "";
            metadata.description = DescBox.Text ?? "";
            ResultMetadata = metadata;
            Close();
        }
        else
        {
            ParallelLogger.Log($"[ERROR] Package name {NameBox.Text} already exists, try another one.");
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void PreviewButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select Preview",
            AllowMultiple = false
        });
        if (files.Count > 0)
        {
            PreviewBox.Text = files[0].Path.LocalPath;
            ThumbnailPath = files[0].Path.LocalPath;
        }
    }

    private bool PackageUpdatable()
    {
        if (string.IsNullOrEmpty(LinkBox.Text)) return false;
        string host = AemulusModManager.Avalonia.Converters.UrlConverter.ConvertUrl(LinkBox.Text);
        return (host == "GameBanana" || host == "GitHub") && !string.IsNullOrEmpty(VersionBox.Text);
    }

}
