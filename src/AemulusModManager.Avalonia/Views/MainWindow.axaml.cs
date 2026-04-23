using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Specialized;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Threading;
using AemulusModManager.Avalonia.ViewModels;
using AemulusModManager.Avalonia.Utilities;
using AemulusModManager.Utilities;
using AemulusModManager.Utilities.PackageUpdating;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace AemulusModManager.Avalonia.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;
    private DialogService? _dialogService;
    private PackageDownloader? _packageDownloader;
    private TextBoxOutputter? _outputter;

    // Browser state
    private bool _browserInitialized;
    private string? _browserGameName;
    private int _page = 1;
    private bool _searched;
    private bool _filterChanging;
    private Dictionary<GameFilter, Dictionary<TypeFilter, List<GameBananaCategory>>>? _cats;

    // Row reorder drag state (mirrors ContextDragBehavior internals)
    private int _dragRowIndex = -1;
    private int _dropFromIndex = -1;   // held across DoDragDrop await
    private Point _dragStartPoint;
    private PointerEventArgs? _triggerEvent;
    private bool _dragLock;
    private bool _dragCaptured;
    public string[]? Args;

    public MainWindow(string[]? args)
    {
        Args = args;
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        SetupConsoleRedirect();
        SetupDragDrop();
        SetupPackageGridDragDrop();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.GameAccentColor))
                Resources["GameAccentIndicator"] = ViewModel.GameAccentColor;
        };
        Resources["GameAccentIndicator"] = ViewModel.GameAccentColor;
        ViewModel.ConsoleEntries.CollectionChanged += (_, e) =>
        {
            var tb = this.FindControl<SelectableTextBlock>("ConsoleText");
            if (tb == null) return;
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                tb.Inlines?.Clear();
            }
            else if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (LogEntry entry in e.NewItems)
                    tb.Inlines?.Add(new Run(entry.Text + "\n") { Foreground = entry.Foreground });
                this.FindControl<ScrollViewer>("ConsoleScroll")?.ScrollToEnd();
            }
        };
    }

    private void SetupPackageGridDragDrop()
    {
        // Attach per-row pointer handlers the same way ContextDragBehavior does:
        // use LoadingRow so each DataGridRow gets the handlers directly.
        PackageGrid.LoadingRow += OnPackageGridRowLoading;
        PackageGrid.UnloadingRow += OnPackageGridRowUnloading;
        PackageGrid.AddHandler(DragDrop.DragOverEvent, OnPackageGridDragOver, AllRoutes, handledEventsToo: true);
        PackageGrid.AddHandler(DragDrop.DropEvent, OnPackageGridDrop, AllRoutes, handledEventsToo: true);
    }

    private static readonly RoutingStrategies AllRoutes =
        RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble;

    private void OnPackageGridRowLoading(object? sender, DataGridRowEventArgs e)
    {
        e.Row.AddHandler(PointerPressedEvent,     OnRowPointerPressed,     AllRoutes, handledEventsToo: true);
        e.Row.AddHandler(PointerMovedEvent,       OnRowPointerMoved,       AllRoutes, handledEventsToo: true);
        e.Row.AddHandler(PointerReleasedEvent,    OnRowPointerReleased,    AllRoutes, handledEventsToo: true);
        e.Row.AddHandler(PointerCaptureLostEvent, OnRowPointerCaptureLost, AllRoutes, handledEventsToo: true);
        e.Row.AddHandler(DragDrop.DragEnterEvent, OnRowDragOver,           AllRoutes, handledEventsToo: true);
        e.Row.AddHandler(DragDrop.DragOverEvent,  OnRowDragOver,           AllRoutes, handledEventsToo: true);
        e.Row.AddHandler(DragDrop.DragLeaveEvent, OnRowDragLeave,          AllRoutes, handledEventsToo: true);
        e.Row.AddHandler(DragDrop.DropEvent,      OnRowDrop,               AllRoutes, handledEventsToo: true);
    }

    private void OnPackageGridRowUnloading(object? sender, DataGridRowEventArgs e)
    {
        e.Row.RemoveHandler(PointerPressedEvent,     OnRowPointerPressed);
        e.Row.RemoveHandler(PointerMovedEvent,       OnRowPointerMoved);
        e.Row.RemoveHandler(PointerReleasedEvent,    OnRowPointerReleased);
        e.Row.RemoveHandler(PointerCaptureLostEvent, OnRowPointerCaptureLost);
        e.Row.RemoveHandler(DragDrop.DragEnterEvent, OnRowDragOver);
        e.Row.RemoveHandler(DragDrop.DragOverEvent,  OnRowDragOver);
        e.Row.RemoveHandler(DragDrop.DragLeaveEvent, OnRowDragLeave);
        e.Row.RemoveHandler(DragDrop.DropEvent,      OnRowDrop);
    }

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DataGridRow row) return;
        if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed) return;

        var list = ViewModel.DisplayedPackages;
        _dragRowIndex = -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], row.DataContext))
            {
                _dragRowIndex = i;
                break;
            }
        }
        if (_dragRowIndex < 0) return;

        // Use root-relative coords (same as ContextDragBehavior: GetPosition(null))
        _dragStartPoint = e.GetPosition(null);
        _triggerEvent   = e;
        _dragLock       = true;
        _dragCaptured   = true;
    }

    private async void OnRowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragCaptured || _triggerEvent == null) return;
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;

        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) <= 3 && Math.Abs(diff.Y) <= 3) return;

        if (_dragLock)
            _dragLock = false;
        else
            return;

        var triggerEvent = _triggerEvent;
        _triggerEvent    = null;
        _dropFromIndex   = _dragRowIndex;

        var data = new DataObject();
        data.Set("aemulus/row", "1");
        await DragDrop.DoDragDrop(triggerEvent, data, DragDropEffects.Move);

        _dropFromIndex = -1;
    }

    private void OnRowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragCaptured) return;
        _triggerEvent  = null;
        _dragLock      = false;
        _dragRowIndex  = -1;
        _dragCaptured  = false;
    }

    private void OnRowPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _triggerEvent  = null;
        _dragLock      = false;
        _dragCaptured  = false;
        _dragRowIndex  = -1;
    }

    private void OnRowDragOver(object? sender, DragEventArgs e)
    {
        if (_dropFromIndex >= 0)
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }
        else if (HasFiles(e.Data))
        {
            e.DragEffects = DragDropEffects.Copy;
            DropOverlay.IsVisible = true;
            e.Handled = true;
        }
    }

    private void OnRowDragLeave(object? sender, RoutedEventArgs e)
    {
        // Only hide the overlay when the cursor leaves a row during a file drag
        // (not during a row reorder). The window-level OnDragLeave handles the
        // case of leaving the window entirely.
        if (_dropFromIndex < 0)
            DropOverlay.IsVisible = false;
    }

    private void OnRowDrop(object? sender, DragEventArgs e)
    {
        var fromIndex  = _dropFromIndex;
        _dropFromIndex = -1;
        if (fromIndex < 0) return;
        e.Handled = true;

        var row = sender as DataGridRow;
        if (row?.DataContext is not DisplayedMetadata destItem) return;
        var list    = ViewModel.DisplayedPackages;
        var toIndex = -1;
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], destItem)) { toIndex = i; break; }
        if (toIndex >= 0 && fromIndex != toIndex)
            ViewModel.MovePackage(fromIndex, toIndex);
    }

    private void OnPackageGridDragOver(object? sender, DragEventArgs e)
    {
        if (_dropFromIndex < 0) return;
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnPackageGridDrop(object? sender, DragEventArgs e)
    {
        // Fallback: catches drops on the DataGrid background (not on a row).
        var fromIndex  = _dropFromIndex;
        _dropFromIndex = -1;
        if (fromIndex < 0) return;
        e.Handled = true;

        var pos     = e.GetPosition(PackageGrid);
        var toIndex = GetRowIndexAtY(pos.Y);
        if (toIndex >= 0 && fromIndex != toIndex)
            ViewModel.MovePackage(fromIndex, toIndex);
    }

    private int GetRowIndexAtY(double yInGrid)
    {
        var list = ViewModel.DisplayedPackages;
        foreach (var row in PackageGrid.GetVisualDescendants().OfType<DataGridRow>())
        {
            var origin = row.TranslatePoint(new Point(0, 0), PackageGrid);
            if (origin == null) continue;
            double top    = origin.Value.Y;
            double bottom = top + row.Bounds.Height;
            if (yInGrid >= top && yInGrid < bottom)
            {
                for (int i = 0; i < list.Count; i++)
                    if (ReferenceEquals(list[i], row.DataContext))
                        return i;
            }
        }
        return -1;
    }

    private void SetupDragDrop()
    {
        AddHandler(DragDrop.DragEnterEvent, OnDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    // Both Avalonia's mapped format AND the raw MIME type Linux file managers send.
    private static bool HasFiles(IDataObject data)
        => data.Contains(DataFormats.Files)
        || data.Contains(DataFormats.Text)
        || data.Contains("text/uri-list");

    private static string[] GetDroppedPaths(IDataObject data)
    {
        // Primary: Avalonia storage items (works on X11 and sometimes Wayland)
        if (data.Contains(DataFormats.Files))
        {
            var files = data.GetFiles();
            if (files != null)
            {
                var paths = files
                    .Select(f => f.TryGetLocalPath())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();
                if (paths.Length > 0) return paths!;
            }
        }

        // Fallback: raw text/uri-list MIME type or plain text.
        // On Linux some DEs send "text/uri-list" without Avalonia mapping it to Files.
        // Split on both \r and \n, skip comment lines, handle file:// and file:///.
        foreach (var fmt in new[] { "text/uri-list", DataFormats.Text })
        {
            if (!data.Contains(fmt)) continue;
            var text = data.Get(fmt) as string ?? data.GetText();
            if (string.IsNullOrEmpty(text)) continue;
            var paths = text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("file://") && !l.StartsWith("#"))
                .Select(l =>
                {
                    try { return new Uri(l).LocalPath; }
                    catch { return null; }
                })
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();
            if (paths.Length > 0) return paths!;
        }

        return Array.Empty<string>();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (_dropFromIndex >= 0) return;
        if (HasFiles(e.Data))
        {
            e.DragEffects = DragDropEffects.Copy;
            DropOverlay.IsVisible = true;
            e.Handled = true;
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (_dropFromIndex < 0)
            DropOverlay.IsVisible = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_dropFromIndex >= 0) return;
        DropOverlay.IsVisible = false;
        e.Handled = true;

        var paths = GetDroppedPaths(e.Data);
        if (paths.Length == 0) return;

        ViewModel.IsUiEnabled = false;
        try
        {
            await ViewModel.ExtractPackages(paths);
            ViewModel.UpdateStats();
        }
        finally
        {
            ViewModel.IsUiEnabled = true;
        }
    }

    private void PackageGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            ViewModel.ToggleSelectedPackageEnabled();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            ViewModel.DeletePackageCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void AddPackage_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel.AddPackagesFromPicker(this);
    }

    private async void CreatePackage_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel.CreateNewPackage(this);
    }

    private void SetupConsoleRedirect()
    {
        var logPath = Path.Combine(Utilities.AppPaths.DataDir, "Aemulus.log");
        var sw = new StreamWriter(logPath, false, Encoding.UTF8, 4096);
        _outputter = new TextBoxOutputter(sw);
        _outputter.WriteEvent += OnConsoleWrite;
        _outputter.WriteLineEvent += OnConsoleWriteLine;
        Console.SetOut(_outputter);
    }

    private void OnConsoleWrite(object? sender, ConsoleWriterEventArgs e)
    {
        Dispatcher.UIThread.Post(() => ViewModel.AppendConsoleRaw(e.Value));
    }

    private void OnConsoleWriteLine(object? sender, ConsoleWriterEventArgs e)
    {
        Dispatcher.UIThread.Post(() => ViewModel.AppendConsole(e.Value));
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _dialogService = new DialogService(this);
        _packageDownloader = new PackageDownloader(_dialogService);
        ViewModel.SetDialogService(_dialogService);
        ViewModel.UpdateStats();
        // Required to ensure colors get set once the dialog service is available.
        if (!ProtocolRegistration.CheckRegistration())
        {
            _dialogService.ShowConfirmation("The Aemulus Protocol is not registered. Would you like to register it now? This is required for some features such as downloading packages directly from GameBanana.").ContinueWith(t =>
            {
                if (t.Result)
                {
                    ProtocolRegistration.RegisterProtocol();
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ViewModel.UpdateGameAccentColor();
                        _ = _dialogService.ShowNotification("Protocol registered! You may now use 1-Click Install links.");
                    });
                }
            });
        }

        ViewModel.UpdateGameAccentColor();
        if(Args != null && Args.Length >= 1){
            if(Args[0].Contains("://")){
                _ = _packageDownloader.DownloadTaskIPC(Args[0]);
            }
        }
    }

    private void SetupGuide_Click(object? sender, PointerPressedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://gamebanana.com/tuts/13379") { UseShellExecute = true }); } catch { }
    }

    private void SupportTekka_Click(object? sender, PointerPressedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://ko-fi.com/tekka") { UseShellExecute = true }); } catch { }
    }

    private void SupportMe_Click(object? sender, PointerPressedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://ko-fi.com/alexankitty") { UseShellExecute = true }); } catch { }
    }

    private async void ConfigButton_Click(object? sender, RoutedEventArgs e)
    {
        var configVm = ViewModel.CreateConfigViewModel();
        var configWindow = new ConfigWindow(configVm);
        configWindow.UnpackRequested += async () =>
        {
            ViewModel.ApplyConfigChanges(configVm);
            await ViewModel.UnpackBaseFiles();
        };
        await configWindow.ShowDialog(this);
        ViewModel.ApplyConfigChanges(configVm);
    }

    private void OnPackageChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is DisplayedMetadata package)
            ViewModel.TogglePackageEnabled(package, true);
    }

    private void OnPackageUnchecked(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is DisplayedMetadata package)
            ViewModel.TogglePackageEnabled(package, false);
    }

    #region Download Tab

    private static readonly GameBananaCategory AllCategory = new() { Name = "All", ID = null };
    private static readonly GameBananaCategory NoneCategory = new() { Name = "- - -", ID = null };

    private static readonly string[] GameIds = { "12961", "8502", "8583", "8263", "15703", "7545", "8464", "17354", "9099", "14377", "9561" };
    private static readonly string[] GameNames = { "Persona 1 (PSP)", "Persona 3 FES", "Persona 3 Portable", "Persona 4 Golden", "Persona 4 Golden (Vita)", "Persona 5", "Persona 5 Royal (PS4)", "Persona 5 Royal (Switch)", "Persona 5 Strikers", "Persona Q", "Persona Q2" };
    private static readonly string[] TypeNames = { "Mod", "Wip", "Sound", "Tool", "Tutorial" };

    private int GetGameFilterIndex()
    {
        var game = ViewModel.SelectedGame;
        var idx = Array.IndexOf(GameNames, game);
        return idx >= 0 ? idx : 0;
    }

    private async void InitializeBrowser()
    {
        if (_browserInitialized && _browserGameName == ViewModel.SelectedGame) return;
        _browserInitialized = true;
        _browserGameName = ViewModel.SelectedGame;
        _filterChanging = true;
        _page = 1;

        GameFilterBox.ItemsSource = GameNames;
        GameFilterBox.SelectedIndex = GetGameFilterIndex();

        TypeBox.ItemsSource = new[] { "Mods", "WiPs", "Sounds", "Tools", "Tutorials" };
        TypeBox.SelectedIndex = 0;

        FilterBox.ItemsSource = new[] { "Featured", "Recent", "Popular" };
        FilterBox.SelectedIndex = 1;

        PerPageBox.ItemsSource = new[] { "10", "25", "50" };
        PerPageBox.SelectedIndex = 0;

        // Fetch categories using apiv4
        _cats = new Dictionary<GameFilter, Dictionary<TypeFilter, List<GameBananaCategory>>>();
        LoadingPanel.IsVisible = true;
        ErrorPanel.IsVisible = false;

        try
        {
            using var httpClient = new HttpClient();
            for (int g = 0; g < GameIds.Length; g++)
            {
                var gameFilter = (GameFilter)g;
                _cats[gameFilter] = new Dictionary<TypeFilter, List<GameBananaCategory>>();
                for (int t = 0; t < TypeNames.Length; t++)
                {
                    var typeFilter = (TypeFilter)t;
                    var allCats = new List<GameBananaCategory>();
                    try
                    {
                        var url = $"https://gamebanana.com/apiv4/{TypeNames[t]}Category/ByGame?_aGameRowIds[]={GameIds[g]}" +
                            "&_sRecordSchema=Custom&_csvProperties=_idRow,_sName,_sProfileUrl,_sIconUrl,_idParentCategoryRow&_nPerpage=50&_bReturnMetadata=true";
                        var responseMsg = await httpClient.GetAsync(url);
                        var responseString = await responseMsg.Content.ReadAsStringAsync();
                        responseString = Regex.Replace(responseString, @"""(\d+)""", @"$1");
                        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<List<GameBananaCategory>>(responseString);
                        if (parsed != null) allCats.AddRange(parsed);

                        // Check for pagination
                        if (responseMsg.Headers.TryGetValues("X-GbApi-Metadata_nRecordCount", out var vals))
                        {
                            if (int.TryParse(vals.FirstOrDefault(), out int count))
                            {
                                int totalPages = (int)Math.Ceiling(count / 50.0);
                                for (int p = 2; p <= totalPages; p++)
                                {
                                    var pageUrl = $"{url}&_nPage={p}";
                                    responseString = await httpClient.GetStringAsync(pageUrl);
                                    responseString = Regex.Replace(responseString, @"""(\d+)""", @"$1");
                                    parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<List<GameBananaCategory>>(responseString);
                                    if (parsed != null) allCats.AddRange(parsed);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ParallelLogger.Log($"[WARNING] Failed to fetch {TypeNames[t]} categories for {GameNames[g]}: {ex.Message}");
                    }
                    _cats[gameFilter][typeFilter] = allCats;
                }
            }
        }
        catch (Exception ex)
        {
            ParallelLogger.Log($"[ERROR] Failed to fetch categories: {ex.Message}");
            ErrorPanel.IsVisible = true;
            BrowserMessage.Text = ex.Message;
            LoadingPanel.IsVisible = false;
            _filterChanging = false;
            return;
        }

        UpdateCategoryBoxes();
        _filterChanging = false;
        await RefreshFilter();
    }

    private void UpdateCategoryBoxes()
    {
        var gameFilter = (GameFilter)GameFilterBox.SelectedIndex;
        var typeFilter = (TypeFilter)TypeBox.SelectedIndex;

        var catItems = new List<GameBananaCategory> { AllCategory };
        if (_cats != null && _cats.TryGetValue(gameFilter, out var types) && types.TryGetValue(typeFilter, out var catList))
            catItems.AddRange(catList.Where(c => c.RootID == 0).OrderBy(c => c.ID));

        CatBox.ItemsSource = catItems;
        CatBox.SelectedIndex = 0;
        SubCatBox.ItemsSource = new List<GameBananaCategory> { NoneCategory };
        SubCatBox.SelectedIndex = 0;
    }

    private void UpdateSubCategoryBox()
    {
        var gameFilter = (GameFilter)GameFilterBox.SelectedIndex;
        var typeFilter = (TypeFilter)TypeBox.SelectedIndex;
        var selectedCat = CatBox.SelectedItem as GameBananaCategory;

        if (selectedCat?.ID != null && _cats != null && _cats.TryGetValue(gameFilter, out var types) && types.TryGetValue(typeFilter, out var catList))
        {
            var subs = catList.Where(c => c.RootID == selectedCat.ID).OrderBy(c => c.ID).ToList();
            if (subs.Any())
            {
                var subItems = new List<GameBananaCategory> { AllCategory };
                subItems.AddRange(subs);
                SubCatBox.ItemsSource = subItems;
            }
            else
                SubCatBox.ItemsSource = new List<GameBananaCategory> { NoneCategory };
        }
        else
            SubCatBox.ItemsSource = new List<GameBananaCategory> { NoneCategory };

        SubCatBox.SelectedIndex = 0;
    }

    private GameFilter GetCurrentGameFilter()
    {
        return (GameFilter)Math.Max(0, GameFilterBox.SelectedIndex);
    }

    private TypeFilter GetCurrentTypeFilter()
    {
        return (TypeFilter)Math.Max(0, TypeBox.SelectedIndex);
    }

    private FeedFilter GetCurrentFeedFilter()
    {
        return (FeedFilter)Math.Max(0, FilterBox.SelectedIndex);
    }

    private async System.Threading.Tasks.Task RefreshFilter()
    {
        LoadingPanel.IsVisible = true;
        ErrorPanel.IsVisible = false;

        var category = CatBox.SelectedItem as GameBananaCategory ?? AllCategory;
        var subcategory = SubCatBox.SelectedItem as GameBananaCategory ?? AllCategory;
        int perPage = (Math.Max(0, PerPageBox.SelectedIndex) + 1) * 10;
        string? search = _searched ? SearchBar.Text : null;

        try
        {
            await FeedGenerator.GetFeed(_page, GetCurrentGameFilter(), GetCurrentTypeFilter(),
                GetCurrentFeedFilter(), category, subcategory, perPage, search);

            if (FeedGenerator.error)
            {
                ErrorPanel.IsVisible = true;
                BrowserMessage.Text = FeedGenerator.exception?.Message ?? "Unknown error";
                FeedBox.ItemsSource = null;
            }
            else
            {
                FeedBox.ItemsSource = FeedGenerator.CurrentFeed.Records;
                _filterChanging = true;
                var pages = new List<string>();
                for (int i = 1; i <= FeedGenerator.CurrentFeed.TotalPages; i++)
                    pages.Add(i.ToString());
                if (pages.Count == 0) pages.Add("1");
                PageBox.ItemsSource = pages;
                PageBox.SelectedIndex = Math.Min(_page - 1, pages.Count - 1);
                _filterChanging = false;
            }
        }
        catch (Exception ex)
        {
            ErrorPanel.IsVisible = true;
            BrowserMessage.Text = ex.Message;
        }

        LoadingPanel.IsVisible = false;
    }

    private void GameFilterSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_filterChanging || !_browserInitialized) return;
        _page = 1;
        _filterChanging = true;
        UpdateCategoryBoxes();
        _filterChanging = false;
        _ = RefreshFilter();
    }

    private void TypeFilterSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_filterChanging || !_browserInitialized) return;
        _page = 1;
        _filterChanging = true;
        UpdateCategoryBoxes();
        _filterChanging = false;
        _ = RefreshFilter();
    }

    private void FilterSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_filterChanging || !_browserInitialized) return;
        _page = 1;
        _ = RefreshFilter();
    }

    private void MainFilterSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_filterChanging || !_browserInitialized) return;
        _filterChanging = true;
        UpdateSubCategoryBox();
        _filterChanging = false;
        _page = 1;
        _ = RefreshFilter();
    }

    private void SubFilterSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_filterChanging || !_browserInitialized) return;
        _page = 1;
        _ = RefreshFilter();
    }

    private void PerPageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_filterChanging || !_browserInitialized) return;
        _page = 1;
        _ = RefreshFilter();
    }

    private void PageBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_filterChanging || !_browserInitialized) return;
        if (PageBox.SelectedIndex >= 0)
        {
            _page = PageBox.SelectedIndex + 1;
            _ = RefreshFilter();
        }
    }

    private void DecrementPage(object? sender, RoutedEventArgs e)
    {
        if (_page > 1)
        {
            _page--;
            _ = RefreshFilter();
        }
    }

    private void IncrementPage(object? sender, RoutedEventArgs e)
    {
        if (_page < (FeedGenerator.CurrentFeed?.TotalPages ?? 1))
        {
            _page++;
            _ = RefreshFilter();
        }
    }

    private void SearchButton_Click(object? sender, RoutedEventArgs e)
    {
        _searched = !string.IsNullOrWhiteSpace(SearchBar.Text);
        _page = 1;
        _ = RefreshFilter();
    }

    private void SearchBar_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _searched = !string.IsNullOrWhiteSpace(SearchBar.Text);
            _page = 1;
            _ = RefreshFilter();
        }
    }

    private async void Download_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GameBananaRecord record && _packageDownloader != null)
        {
            var gameName = GameNames[Math.Max(0, GameFilterBox.SelectedIndex)];
            await _packageDownloader.BrowserDownload(record, gameName);
            ViewModel.RefreshPackageList();
        }
    }

    private async void MoreInfo_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GameBananaRecord record)
        {
            var desc = record.Text ?? record.Description ?? "No description available.";
            var box = new NotificationBox(_dialogService.DialogWindow ,$"{record.Title}\n\nby {record.Owner?.Name}\n\n{desc}", true);
            await box.ShowDialog(this);
        }
    }

    private void Homepage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GameBananaRecord record && record.Link != null)
        {
            Process.Start(new ProcessStartInfo(record.Link.ToString()) { UseShellExecute = true });
        }
    }

    private void TabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TabControl tc && tc.SelectedIndex == 1)
        {
            InitializeBrowser();
        }
    }

    private async void BrowserRefresh_Click(object? sender, RoutedEventArgs e)
    {
        await RefreshFilter();
    }

    #endregion
}
