using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AemulusModManager.Utilities;
using AemulusModManager.Utilities.AwbMerging;
using AemulusModManager.Avalonia.Utilities;
using FileMerging = AemulusModManager.Avalonia.Utilities.FileMerging;

namespace AemulusModManager.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    // Serializers
    private readonly XmlSerializer _xsp = new(typeof(Metadata));
    private readonly XmlSerializer _xsm = new(typeof(ModXmlMetadata));
    private readonly XmlSerializer _xp = new(typeof(Packages));

    // State
    public AemulusConfig Config { get; set; } = new();
    private ObservableCollection<Package> _packageList = new();
    private readonly string _exeDir;
    private readonly string _dataDir;
    private readonly string _configPath;

    // Per-game settings (loaded from config)
    public string? ModPath { get; set; }
    public string? GamePath { get; set; }
    public string? LauncherPath { get; set; }
    public string? ElfPath { get; set; }
    public string? CpkLang { get; set; }
    public string? CpkPath { get; set; }
    public string? CheatsPath { get; set; }
    public string? CheatsWSPath { get; set; }
    public string? TexturesPath { get; set; }
    public string? CpkName { get; set; }
    public string? GameVersion { get; set; }
    public bool EmptySND { get; set; }
    public bool UseCpk { get; set; }
    public bool BuildWarning { get; set; }
    public bool BuildFinished { get; set; }
    public bool UpdateChangelog { get; set; }
    public bool UpdateAll { get; set; }
    public bool UpdatesEnabled { get; set; }
    public bool DeleteOldVersions { get; set; }
    public bool CreateIso { get; set; }
    public bool AdvancedLaunchOptions { get; set; }

    [ObservableProperty] private string _windowTitle = "Aemulus Package Manager";
    [ObservableProperty] private ObservableCollection<DisplayedMetadata> _displayedPackages = new();
    [ObservableProperty] private DisplayedMetadata? _selectedPackage;
    [ObservableProperty] private string _selectedGame = "Persona 4 Golden";
    [ObservableProperty] private ObservableCollection<string> _gameList = new()
    {
        "Persona 1 (PSP)", "Persona 3 FES", "Persona 3 Portable",
        "Persona 4 Golden", "Persona 4 Golden (Vita)", "Persona 5",
        "Persona 5 Royal (PS4)", "Persona 5 Royal (Switch)",
        "Persona 5 Strikers", "Persona Q", "Persona Q2",
    };
    [ObservableProperty] private ObservableCollection<string> _loadoutItems = new() { "Default", "Add new loadout" };
    [ObservableProperty] private string _selectedLoadout = "Default";
    private string _lastLoadout = "Default";
    private bool _loadoutChanging;
    [ObservableProperty] private bool _bottomUpPriority;
    [ObservableProperty] private string _priorityLabel = "higher priority ▲";
    [ObservableProperty] private string _consoleOutput = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _showHiddenPackages = true;
    [ObservableProperty] private bool _isBuildEnabled = true;
    [ObservableProperty] private bool _isUiEnabled = true;
    [ObservableProperty] private string _packageStats = "";
    [ObservableProperty] private global::Avalonia.Media.IBrush _gameAccentColor = global::Avalonia.Media.SolidColorBrush.Parse("#F5E63D");

    private DialogService? _dialogService;
    private FileSystemWatcher? _fileSystemWatcher;
    private CancellationTokenSource _cancellationToken = new();
    private bool _updating;
    [ObservableProperty] private bool _darkMode = true;

    // Theme-reactive color properties
    [ObservableProperty] private IBrush _themeBg = SolidColorBrush.Parse("#151515");
    [ObservableProperty] private IBrush _themeSurface = SolidColorBrush.Parse("#202020");
    [ObservableProperty] private IBrush _themeControl = SolidColorBrush.Parse("#303030");
    [ObservableProperty] private IBrush _themeBorder = SolidColorBrush.Parse("#404040");
    [ObservableProperty] private IBrush _themeFg = SolidColorBrush.Parse("#f2f2f2");
    [ObservableProperty] private IBrush _themeMuted = SolidColorBrush.Parse("#9c9c9c");
    [ObservableProperty] private IBrush _themeConsoleBg = SolidColorBrush.Parse("#101010");

    private void ApplyThemeColors()
    {
        if (DarkMode)
        {
            ThemeBg = SolidColorBrush.Parse("#151515");
            ThemeSurface = SolidColorBrush.Parse("#202020");
            ThemeControl = SolidColorBrush.Parse("#303030");
            ThemeBorder = SolidColorBrush.Parse("#404040");
            ThemeFg = SolidColorBrush.Parse("#f2f2f2");
            ThemeMuted = SolidColorBrush.Parse("#9c9c9c");
            ThemeConsoleBg = SolidColorBrush.Parse("#101010");
        }
        else
        {
            ThemeBg = SolidColorBrush.Parse("#f0f0f0");
            ThemeSurface = SolidColorBrush.Parse("#ffffff");
            ThemeControl = SolidColorBrush.Parse("#e0e0e0");
            ThemeBorder = SolidColorBrush.Parse("#cccccc");
            ThemeFg = SolidColorBrush.Parse("#1a1a1a");
            ThemeMuted = SolidColorBrush.Parse("#666666");
            ThemeConsoleBg = SolidColorBrush.Parse("#f8f8f8");
        }
    }
    private static readonly string _appVersion = System.Diagnostics.FileVersionInfo
        .GetVersionInfo(Assembly.GetEntryAssembly()?.Location ?? "").FileVersion ?? "1.0.0.0";

    public void SetDialogService(DialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public MainWindowViewModel()
    {
        _exeDir = Utilities.AppPaths.ExeDir;
        _dataDir = Utilities.AppPaths.DataDir;
        _configPath = Utilities.AppPaths.ConfigDir;
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_configPath);

        LoadAemulusConfig();
        LoadGameConfig();
        LoadPackages();
        UpdateGameAccentColor();
        SetupFileSystemWatcher();
        AppendConsole("[INFO] Aemulus Package Manager started (Avalonia cross-platform build)");
    }

    private void SetupFileSystemWatcher()
    {
        try
        {
            _fileSystemWatcher = new FileSystemWatcher(_dataDir)
            {
                Filter = "refresh.aem",
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
            };
            _fileSystemWatcher.Created += OnRefreshFileCreated;
        }
        catch (Exception ex)
        {
            AppendConsole($"[WARNING] FileSystemWatcher setup failed: {ex.Message}");
        }
    }

    private async void OnRefreshFileCreated(object sender, FileSystemEventArgs e)
    {
        var refreshPath = Path.Combine(_dataDir, "refresh.aem");
        if (!File.Exists(refreshPath)) return;

        // Wait for file to be ready
        for (int i = 0; i < 50; i++)
        {
            try
            {
                using var fs = File.Open(refreshPath, FileMode.Open, FileAccess.Read, FileShare.None);
                if (fs.Length > 0) break;
            }
            catch (IOException) { await Task.Delay(100); }
        }

        try
        {
            var game = File.ReadAllText(refreshPath).Trim();
            File.Delete(refreshPath);

            var configTemp = Path.Combine(_configPath, "temp");
            if (Directory.Exists(configTemp))
            {
                await Task.Run(() => ReplacePackagesXML(game));
            }
            else
            {
                await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshPackageList();
                    SavePackages();
                });
            }
        }
        catch (Exception ex)
        {
            AppendConsole($"[ERROR] FileSystemWatcher handler: {ex.Message}");
        }
    }

    private void ReplacePackagesXML(string? game = null)
    {
        game ??= SelectedGame;
        var configTemp = Path.Combine(_configPath, "temp");
        if (!Directory.Exists(configTemp)) return;

        foreach (var file in Directory.GetFiles(configTemp))
        {
            var destDir = Path.Combine(_configPath, game);
            Directory.CreateDirectory(destDir);
            var dest = Path.Combine(destDir, Path.GetFileName(file));
            try
            {
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(file, dest);
            }
            catch (Exception ex)
            {
                AppendConsole($"[ERROR] Failed to move {file}: {ex.Message}");
            }
        }

        try { Directory.Delete(configTemp, true); } catch { }

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RefreshPackageList();
            SavePackages();
        });
    }

    #region Config Loading/Saving

    private void LoadAemulusConfig()
    {
        var configFile = Path.Combine(_configPath, "Config.xml");
        Directory.CreateDirectory(_configPath);

        if (File.Exists(configFile))
        {
            try
            {
                using var stream = File.OpenRead(configFile);
                var serializer = new XmlSerializer(typeof(AemulusConfig));
                Config = (AemulusConfig?)serializer.Deserialize(stream) ?? new AemulusConfig();
            }
            catch (Exception ex)
            {
                AppendConsole($"[ERROR] Failed to load config: {ex.Message}");
                Config = new AemulusConfig();
            }
        }

        BottomUpPriority = Config.bottomUpPriority;
        PriorityLabel = BottomUpPriority ? "higher priority ▲" : "higher priority ▼";
        DarkMode = Config.darkMode;
        if (!string.IsNullOrEmpty(Config.game))
            SelectedGame = Config.game;

        // Apply theme on startup
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = DarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        ApplyThemeColors();
    }

    public void UpdateConfig()
    {
        Config.game = SelectedGame;
        Config.bottomUpPriority = BottomUpPriority;

        Directory.CreateDirectory(_configPath);
        var configFile = Path.Combine(_configPath, "Config.xml");
        try
        {
            using var stream = File.Create(configFile);
            new XmlSerializer(typeof(AemulusConfig)).Serialize(stream, Config);
        }
        catch (Exception ex)
        {
            AppendConsole($"[ERROR] Failed to save config: {ex.Message}");
        }
    }

    private void LoadGameConfig()
    {
        // Reset optional fields
        CheatsPath = null; CheatsWSPath = null; TexturesPath = null;
        ElfPath = null; CpkName = null; GameVersion = null;
        EmptySND = false; UseCpk = false; CreateIso = false; AdvancedLaunchOptions = false;

        switch (SelectedGame)
        {
            case "Persona 1 (PSP)":
                Config.p1pspConfig ??= new ConfigP1PSP();
                ModPath = Config.p1pspConfig.modDir;
                GamePath = Config.p1pspConfig.isoPath;
                LauncherPath = Config.p1pspConfig.launcherPath;
                TexturesPath = Config.p1pspConfig.texturesPath;
                CheatsPath = Config.p1pspConfig.cheatsPath;
                CreateIso = Config.p1pspConfig.createIso;
                BuildFinished = Config.p1pspConfig.buildFinished;
                BuildWarning = Config.p1pspConfig.buildWarning;
                UpdateChangelog = Config.p1pspConfig.updateChangelog;
                DeleteOldVersions = Config.p1pspConfig.deleteOldVersions;
                UpdatesEnabled = Config.p1pspConfig.updatesEnabled;
                UpdateAll = Config.p1pspConfig.updateAll;
                break;
            case "Persona 3 FES":
                Config.p3fConfig ??= new ConfigP3F();
                ModPath = Config.p3fConfig.modDir;
                GamePath = Config.p3fConfig.isoPath;
                ElfPath = Config.p3fConfig.elfPath;
                LauncherPath = Config.p3fConfig.launcherPath;
                CheatsPath = Config.p3fConfig.cheatsPath;
                CheatsWSPath = Config.p3fConfig.cheatsWSPath;
                TexturesPath = Config.p3fConfig.texturesPath;
                AdvancedLaunchOptions = Config.p3fConfig.advancedLaunchOptions;
                BuildFinished = Config.p3fConfig.buildFinished;
                BuildWarning = Config.p3fConfig.buildWarning;
                UpdateChangelog = Config.p3fConfig.updateChangelog;
                DeleteOldVersions = Config.p3fConfig.deleteOldVersions;
                UpdatesEnabled = Config.p3fConfig.updatesEnabled;
                UpdateAll = Config.p3fConfig.updateAll;
                break;
            case "Persona 3 Portable":
                Config.p3pConfig ??= new ConfigP3P();
                ModPath = Config.p3pConfig.modDir;
                GamePath = Config.p3pConfig.isoPath;
                LauncherPath = Config.p3pConfig.launcherPath;
                TexturesPath = Config.p3pConfig.texturesPath;
                CheatsPath = Config.p3pConfig.cheatsPath;
                CpkName = Config.p3pConfig.cpkName;
                BuildFinished = Config.p3pConfig.buildFinished;
                BuildWarning = Config.p3pConfig.buildWarning;
                UpdateChangelog = Config.p3pConfig.updateChangelog;
                DeleteOldVersions = Config.p3pConfig.deleteOldVersions;
                UpdatesEnabled = Config.p3pConfig.updatesEnabled;
                UpdateAll = Config.p3pConfig.updateAll;
                break;
            case "Persona 4 Golden":
                Config.p4gConfig ??= new ConfigP4G();
                ModPath = Config.p4gConfig.modDir;
                GamePath = Config.p4gConfig.exePath;
                LauncherPath = Config.p4gConfig.reloadedPath;
                EmptySND = Config.p4gConfig.emptySND;
                UseCpk = Config.p4gConfig.useCpk;
                CpkLang = Config.p4gConfig.cpkLang;
                BuildFinished = Config.p4gConfig.buildFinished;
                BuildWarning = Config.p4gConfig.buildWarning;
                UpdateChangelog = Config.p4gConfig.updateChangelog;
                DeleteOldVersions = Config.p4gConfig.deleteOldVersions;
                UpdatesEnabled = Config.p4gConfig.updatesEnabled;
                UpdateAll = Config.p4gConfig.updateAll;
                break;
            case "Persona 4 Golden (Vita)":
                Config.p4gVitaConfig ??= new ConfigP4GVita();
                ModPath = Config.p4gVitaConfig.modDir;
                CpkName = Config.p4gVitaConfig.cpkName;
                BuildFinished = Config.p4gVitaConfig.buildFinished;
                BuildWarning = Config.p4gVitaConfig.buildWarning;
                UpdateChangelog = Config.p4gVitaConfig.updateChangelog;
                DeleteOldVersions = Config.p4gVitaConfig.deleteOldVersions;
                UpdatesEnabled = Config.p4gVitaConfig.updatesEnabled;
                UpdateAll = Config.p4gVitaConfig.updateAll;
                break;
            case "Persona 5":
                Config.p5Config ??= new ConfigP5();
                ModPath = Config.p5Config.modDir;
                GamePath = Config.p5Config.gamePath;
                LauncherPath = Config.p5Config.launcherPath;
                CpkName = Config.p5Config.CpkName;
                BuildFinished = Config.p5Config.buildFinished;
                BuildWarning = Config.p5Config.buildWarning;
                UpdateChangelog = Config.p5Config.updateChangelog;
                DeleteOldVersions = Config.p5Config.deleteOldVersions;
                UpdatesEnabled = Config.p5Config.updatesEnabled;
                UpdateAll = Config.p5Config.updateAll;
                break;
            case "Persona 5 Royal (PS4)":
                Config.p5rConfig ??= new ConfigP5R();
                ModPath = Config.p5rConfig.modDir;
                CpkName = Config.p5rConfig.cpkName;
                CpkLang = Config.p5rConfig.language;
                GameVersion = Config.p5rConfig.version;
                BuildFinished = Config.p5rConfig.buildFinished;
                BuildWarning = Config.p5rConfig.buildWarning;
                UpdateChangelog = Config.p5rConfig.updateChangelog;
                DeleteOldVersions = Config.p5rConfig.deleteOldVersions;
                UpdatesEnabled = Config.p5rConfig.updatesEnabled;
                UpdateAll = Config.p5rConfig.updateAll;
                break;
            case "Persona 5 Royal (Switch)":
                Config.p5rSwitchConfig ??= new ConfigP5RSwitch();
                ModPath = Config.p5rSwitchConfig.modDir;
                GamePath = Config.p5rSwitchConfig.gamePath;
                LauncherPath = Config.p5rSwitchConfig.launcherPath;
                CpkLang = Config.p5rSwitchConfig.language;
                BuildFinished = Config.p5rSwitchConfig.buildFinished;
                BuildWarning = Config.p5rSwitchConfig.buildWarning;
                UpdateChangelog = Config.p5rSwitchConfig.updateChangelog;
                DeleteOldVersions = Config.p5rSwitchConfig.deleteOldVersions;
                UpdatesEnabled = Config.p5rSwitchConfig.updatesEnabled;
                UpdateAll = Config.p5rSwitchConfig.updateAll;
                break;
            case "Persona 5 Strikers":
                Config.p5sConfig ??= new ConfigP5S();
                ModPath = Config.p5sConfig.modDir;
                BuildFinished = Config.p5sConfig.buildFinished;
                BuildWarning = Config.p5sConfig.buildWarning;
                UpdateChangelog = Config.p5sConfig.updateChangelog;
                DeleteOldVersions = Config.p5sConfig.deleteOldVersions;
                UpdatesEnabled = Config.p5sConfig.updatesEnabled;
                UpdateAll = Config.p5sConfig.updateAll;
                break;
            case "Persona Q":
                Config.pqConfig ??= new ConfigPQ();
                ModPath = Config.pqConfig.modDir;
                GamePath = Config.pqConfig.ROMPath;
                LauncherPath = Config.pqConfig.launcherPath;
                BuildFinished = Config.pqConfig.buildFinished;
                BuildWarning = Config.pqConfig.buildWarning;
                UpdateChangelog = Config.pqConfig.updateChangelog;
                DeleteOldVersions = Config.pqConfig.deleteOldVersions;
                UpdatesEnabled = Config.pqConfig.updatesEnabled;
                UpdateAll = Config.pqConfig.updateAll;
                break;
            case "Persona Q2":
                Config.pq2Config ??= new ConfigPQ2();
                ModPath = Config.pq2Config.modDir;
                GamePath = Config.pq2Config.ROMPath;
                LauncherPath = Config.pq2Config.launcherPath;
                BuildFinished = Config.pq2Config.buildFinished;
                BuildWarning = Config.pq2Config.buildWarning;
                UpdateChangelog = Config.pq2Config.updateChangelog;
                DeleteOldVersions = Config.pq2Config.deleteOldVersions;
                UpdatesEnabled = Config.pq2Config.updatesEnabled;
                UpdateAll = Config.pq2Config.updateAll;
                break;
        }

        IsBuildEnabled = !string.IsNullOrEmpty(ModPath);
    }

    #endregion

    #region Package Management

    private void LoadPackages()
    {
        var packagesDir = Path.Combine(_dataDir, "Packages", SelectedGame);
        Directory.CreateDirectory(packagesDir);

        // Load saved package list
        var loadoutFile = Path.Combine(_configPath, SelectedGame, $"{SelectedLoadout}.xml");
        Directory.CreateDirectory(Path.Combine(_configPath, SelectedGame));

        _packageList = new ObservableCollection<Package>();
        if (File.Exists(loadoutFile))
        {
            try
            {
                using var stream = File.OpenRead(loadoutFile);
                var packages = (Packages?)_xp.Deserialize(stream);
                if (packages?.packages != null)
                    _packageList = new ObservableCollection<Package>(packages.packages);
            }
            catch (Exception ex)
            {
                AppendConsole($"[ERROR] Failed to load packages: {ex.Message}");
            }
        }

        // Load loadout list
        var loadouts = new Utilities.Loadouts(SelectedGame);
        var targetLoadout = SelectedLoadout;
        _loadoutChanging = true;
        LoadoutItems = loadouts.LoadoutItems;
        if (LoadoutItems.Contains(targetLoadout))
            SelectedLoadout = targetLoadout;
        else if (LoadoutItems.Count > 1)
            SelectedLoadout = LoadoutItems[0];
        _lastLoadout = SelectedLoadout;
        _loadoutChanging = false;

        // Build displayed packages
        RefreshPackageList();
    }

    public void RefreshPackageList()
    {
        var packagesDir = Path.Combine(_dataDir, "Packages", SelectedGame);
        Directory.CreateDirectory(packagesDir);
        var tempDisplayed = new List<DisplayedMetadata>();

        // Remove deleted packages
        foreach (var package in _packageList.ToList())
        {
            if (!Directory.Exists(Path.Combine(packagesDir, package.path)))
            {
                _packageList.Remove(package);
                continue;
            }

            var xmlPath = Path.Combine(packagesDir, package.path, "Package.xml");
            if (File.Exists(xmlPath))
            {
                try
                {
                    using var stream = File.OpenRead(xmlPath);
                    var metadata = (Metadata?)_xsp.Deserialize(stream);
                    if (metadata != null)
                    {
                        package.id = metadata.id;
                        var dm = InitDisplayedMetadata(metadata);
                        dm.enabled = package.enabled;
                        dm.path = package.path;
                        tempDisplayed.Add(dm);
                    }
                }
                catch (Exception ex)
                {
                    AppendConsole($"[ERROR] Invalid Package.xml for {package.path} ({ex.Message})");
                }
            }
            else
            {
                var dm = new DisplayedMetadata { name = package.path, path = package.path, enabled = package.enabled };
                tempDisplayed.Add(dm);
            }
        }

        // Add new packages from directory
        foreach (var dir in Directory.GetDirectories(packagesDir))
        {
            var dirName = Path.GetFileName(dir);
            if (_packageList.Any(x => x.path == dirName))
                continue;

            var xmlPath = Path.Combine(dir, "Package.xml");
            Metadata? metadata = null;
            if (File.Exists(xmlPath))
            {
                try
                {
                    using var stream = File.OpenRead(xmlPath);
                    metadata = (Metadata?)_xsp.Deserialize(stream);
                }
                catch (Exception ex)
                {
                    AppendConsole($"[ERROR] Invalid Package.xml for {dirName} ({ex.Message})");
                }
            }

            if (metadata == null)
            {
                metadata = new Metadata { name = dirName, id = dirName.Replace(" ", "").ToLower() };
                // Create Package.xml
                try
                {
                    // Check for Mod Compendium structure
                    var modXml = Path.Combine(dir, "Mod.xml");
                    if (File.Exists(modXml) && Directory.Exists(Path.Combine(dir, "Data")))
                    {
                        AppendConsole($"[INFO] Converting {dirName} from Mod Compendium structure...");
                        using var modStream = File.OpenRead(modXml);
                        var modMeta = (ModXmlMetadata?)_xsm.Deserialize(modStream);
                        if (modMeta != null)
                        {
                            metadata.id = (modMeta.Author?.ToLower().Replace(" ", "") ?? "") + "."
                                        + (modMeta.Title?.ToLower().Replace(" ", "") ?? "");
                            metadata.author = modMeta.Author ?? "";
                            metadata.version = modMeta.Version ?? "";
                            metadata.link = modMeta.Url ?? "";
                            metadata.description = modMeta.Description ?? "";
                        }
                    }

                    using var writeStream = File.Create(Path.Combine(dir, "Package.xml"));
                    _xsp.Serialize(writeStream, metadata);
                }
                catch (Exception ex)
                {
                    AppendConsole($"[ERROR] Couldn't create Package.xml for {dirName} ({ex.Message})");
                }
            }

            var newPackage = new Package
            {
                enabled = false,
                id = metadata.id,
                path = dirName,
                name = metadata.name,
                link = metadata.link
            };
            _packageList.Add(newPackage);

            var displayedMeta = InitDisplayedMetadata(metadata);
            displayedMeta.enabled = false;
            displayedMeta.path = dirName;
            tempDisplayed.Add(displayedMeta);
        }

        DisplayedPackages = new ObservableCollection<DisplayedMetadata>(tempDisplayed);
        SavePackages();
    }

    public DisplayedMetadata InitDisplayedMetadata(Metadata m)
    {
        var dm = new DisplayedMetadata
        {
            name = m.name,
            id = m.id,
            author = m.author,
            description = m.description,
            link = m.link,
            skippedVersion = m.skippedVersion
        };
        if (Version.TryParse(m.version, out _))
            dm.version = m.version;
        return dm;
    }

    private void SavePackages()
    {
        var loadoutFile = Path.Combine(_configPath, SelectedGame, $"{SelectedLoadout}.xml");
        Directory.CreateDirectory(Path.Combine(_configPath, SelectedGame));
        try
        {
            var packages = new Packages { packages = new ObservableCollection<Package>(_packageList) };
            using var stream = File.Create(loadoutFile);
            _xp.Serialize(stream, packages);
        }
        catch (Exception ex)
        {
            AppendConsole($"[ERROR] Failed to save packages: {ex.Message}");
        }
    }

    public void UpdatePackages()
    {
        SavePackages();
    }

    public void TogglePackageEnabled(DisplayedMetadata package, bool enabled)
    {
        package.enabled = enabled;
        foreach (var p in _packageList.Where(p => p.path == package.path))
            p.enabled = enabled;
        UpdatePackages();
    }

    public void MovePackage(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || newIndex < 0 ||
            oldIndex >= DisplayedPackages.Count || newIndex >= DisplayedPackages.Count)
            return;

        DisplayedPackages.Move(oldIndex, newIndex);

        // Rebuild package list order
        var newOrder = new ObservableCollection<Package>();
        foreach (var displayed in DisplayedPackages)
        {
            var pkg = _packageList.FirstOrDefault(p => p.path == displayed.path);
            if (pkg != null) newOrder.Add(pkg);
        }
        _packageList = newOrder;
        SavePackages();
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void SwapPriority()
    {
        BottomUpPriority = !BottomUpPriority;
        PriorityLabel = BottomUpPriority ? "higher priority ▲" : "higher priority ▼";
        UpdateConfig();
        AppendConsole($"[INFO] Priority set to {(BottomUpPriority ? "bottom-up" : "top-down")}");
    }

    [RelayCommand]
    private void ChangeGame()
    {
        Config.game = SelectedGame;
        UpdateConfig();
        LoadGameConfig();
        LoadPackages();
        UpdateGameAccentColor();
        UpdateStats();
        AppendConsole($"[INFO] Switched to {SelectedGame}");
    }

    partial void OnSelectedGameChanged(string value)
    {
        ChangeGame();
    }

    partial void OnSelectedLoadoutChanged(string value)
    {
        if (_loadoutChanging) return;
        if (value == "Add new loadout")
        {
            _loadoutChanging = true;
            _ = HandleNewLoadoutAsync();
            return;
        }
        if (!string.IsNullOrEmpty(value))
        {
            _lastLoadout = value;
            UpdateConfig();
            LoadPackages();
            AppendConsole($"[INFO] Switched to loadout {value}");
        }
    }

    private async Task HandleNewLoadoutAsync()
    {
        // _loadoutChanging is already true from OnSelectedLoadoutChanged
        try
        {
            if (_dialogService == null)
            {
                SelectedLoadout = _lastLoadout;
                return;
            }

            var (name, copyLoadout) = await _dialogService.ShowInputDialog("Enter loadout name:");
            if (string.IsNullOrWhiteSpace(name) || name == "Add new loadout")
            {
                SelectedLoadout = _lastLoadout;
                return;
            }

            // Check for duplicate
            var loadoutFile = Path.Combine(_configPath, SelectedGame, $"{name}.xml");
            if (File.Exists(loadoutFile))
            {
                await _dialogService.ShowNotification($"A loadout named \"{name}\" already exists.");
                SelectedLoadout = _lastLoadout;
                return;
            }

            // Copy current loadout if requested, otherwise create an empty file
            if (copyLoadout)
            {
                var currentFile = Path.Combine(_configPath, SelectedGame, $"{_lastLoadout}.xml");
                if (File.Exists(currentFile))
                    File.Copy(currentFile, loadoutFile);
            }
            if (!File.Exists(loadoutFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(loadoutFile)!);
                var empty = new Packages { packages = new ObservableCollection<Package>() };
                using var stream = File.Create(loadoutFile);
                _xp.Serialize(stream, empty);
            }

            // Set the backing field directly so LoadPackages picks up the right name
            // without triggering OnSelectedLoadoutChanged again.
            _selectedLoadout = name;
            _lastLoadout = name;
            UpdateConfig();
            _loadoutChanging = false;
            LoadPackages();
            AppendConsole($"[INFO] Created loadout {name}");
        }
        finally
        {
            _loadoutChanging = false;
        }
    }

    [RelayCommand]
    private async Task Build()
    {
        AppendConsole($"[INFO] Building for {SelectedGame}...");
        IsBuildEnabled = false;
        IsUiEnabled = false;
        try
        {
            if (string.IsNullOrEmpty(ModPath) || !Directory.Exists(ModPath))
            {
                AppendConsole("[ERROR] Output folder not set or doesn't exist. Please configure it first.");
                return;
            }

            // Check for base files and unpack if needed
            if (!await EnsureBaseFilesExist())
                return;

            // Build package list (paths to enabled package dirs)
            var packages = new List<string>();
            var game = SelectedGame;
            var assemblyLocation = _dataDir;
            foreach (var m in _packageList.ToList())
            {
                if (m.enabled)
                {
                    packages.Add(Path.Combine(assemblyLocation, "Packages", game, m.path ?? ""));
                    ParallelLogger.Log($"[INFO] Using {m.path} in loadout");
                    // P4G auto-detect CPK structure
                    if (game == "Persona 4 Golden" && !UseCpk)
                    {
                        var pkgDir = Path.Combine(assemblyLocation, "Packages", game, m.path ?? "");
                        if ((CpkLang != null && Directory.Exists(Path.Combine(pkgDir, Path.GetFileNameWithoutExtension(CpkLang))))
                            || Directory.Exists(Path.Combine(pkgDir, "movie"))
                            || Directory.Exists(Path.Combine(pkgDir, "preappfile")))
                        {
                            ParallelLogger.Log($"[WARNING] {m.path} is using CPK folder paths, setting Use CPK Structure to true");
                            UseCpk = true;
                        }
                    }
                }
            }
            if (!BottomUpPriority)
                packages.Reverse();

            if (packages.Count == 0)
            {
                AppendConsole("[WARNING] No packages enabled in loadout, emptying output folder...");
                var emptyPath = GetOutputPath(game);

                // BuildWarning confirmation
                if (BuildWarning && Directory.Exists(emptyPath) && Directory.EnumerateFileSystemEntries(emptyPath).Any())
                {
                    if (_dialogService != null)
                    {
                        var yes = await _dialogService.ShowNotification(
                            $"Confirm DELETING THE ENTIRE CONTENTS of {emptyPath} before building?", false);
                        if (!yes)
                        {
                            ParallelLogger.Log("[INFO] Cancelled build");
                            return;
                        }
                    }
                }

                // Delete old CPK/ISO files
                DeleteOldBuildOutputs(game);

                // Empty output folder
                if (game == "Persona 5 Strikers")
                    global::AemulusModManager.Utilities.KT.Merger.Restart(emptyPath);
                else if (game == "Persona Q2" || game == "Persona Q")
                    binMerge.Restart(emptyPath, EmptySND, game, CpkLang ?? "", CheatsPath, CheatsWSPath, true);
                else
                    binMerge.Restart(emptyPath, EmptySND, game, CpkLang ?? "", CheatsPath, CheatsWSPath);
                AppendConsole("[INFO] Finished emptying output folder!");
                if (BuildFinished && _dialogService != null)
                    await _dialogService.ShowNotification("Finished emptying output folder!");
                return;
            }

            // BuildWarning confirmation for full build
            var buildPath = GetOutputPath(game);
            if (BuildWarning && Directory.Exists(buildPath) && Directory.EnumerateFileSystemEntries(buildPath).Any())
            {
                if (_dialogService != null)
                {
                    var yes = await _dialogService.ShowNotification(
                        $"Confirm DELETING THE ENTIRE CONTENTS of {buildPath} before building?", false);
                    if (!yes)
                    {
                        ParallelLogger.Log("[INFO] Cancelled build");
                        return;
                    }
                }
            }

            AppendConsole($"[INFO] {packages.Count} packages enabled for merge");

            var buildTimer = System.Diagnostics.Stopwatch.StartNew();

            if (game == "Persona 5 Strikers")
            {
                // P5S uses KT Merger
                await Task.Run(() =>
                {
                    var path = ModPath;
                    global::AemulusModManager.Utilities.KT.Merger.Restart(path);
                    global::AemulusModManager.Utilities.KT.Merger.Merge(packages, path);
                    global::AemulusModManager.Utilities.KT.Merger.Patch(path);
                });
            }
            else if (game == "Persona 1 (PSP)")
            {
                // P1PSP: simpler path
                await Task.Run(() =>
                {
                    var path = Path.Combine(ModPath, "Persona 1 (PSP)", "PSP_GAME");
                    Directory.CreateDirectory(path);
                    binMerge.Restart(path, EmptySND, game, CpkLang ?? "", null, null);
                    binMerge.Unpack(packages, path, UseCpk, CpkLang ?? "", game);
                    // Copy unchanged files from Original
                    var origDir = Path.Combine(assemblyLocation, "Original", "Persona 1 (PSP)");
                    if (Directory.Exists(origDir))
                    {
                        ParallelLogger.Log("[INFO] Adding unchanged files...");
                        foreach (var file in Directory.GetFiles(origDir, "*.*", SearchOption.AllDirectories))
                        {
                            var relPath = Path.GetRelativePath(origDir, file);
                            var binPath = Path.Combine(ModPath, "Persona 1 (PSP)", relPath);
                            Directory.CreateDirectory(Path.GetDirectoryName(binPath)!);
                            if (!File.Exists(binPath))
                                File.Copy(file, binPath, false);
                        }
                    }
                    if (packages.Exists(x => Directory.Exists(Path.Combine(x, "binarypatches"))))
                        BinaryPatcher.Patch(packages, Path.Combine(ModPath, "Persona 1 (PSP)"), UseCpk, CpkLang ?? "", game);
                });
                // P1PSP cheats
                if (packages.Exists(x => Directory.Exists(Path.Combine(x, "cheats"))))
                {
                    if (Config.p1pspConfig?.cheatsPath != null)
                        binMerge.LoadP1PSPCheats(packages, Config.p1pspConfig.cheatsPath);
                    else
                    {
                        // Auto-detect
                        if (Config.p1pspConfig?.modDir != null)
                        {
                            var modDir = new DirectoryInfo(Config.p1pspConfig.modDir);
                            var cheatsDir = modDir.Parent?.EnumerateDirectories("Cheats").FirstOrDefault();
                            var cheatIni = cheatsDir?.EnumerateFiles("*.ini").FirstOrDefault(f => f.Name == "ULUS10432.ini");
                            if (cheatIni != null)
                                binMerge.LoadP1PSPCheats(packages, cheatIni.FullName);
                            else
                                ParallelLogger.Log("[ERROR] Unable to automatically determine cheats path, please set up cheats path in config");
                        }
                    }
                }
                if (packages.Exists(x => Directory.Exists(Path.Combine(x, "texture_override"))))
                {
                    if (Config.p1pspConfig?.texturesPath != null && Directory.Exists(Config.p1pspConfig.texturesPath))
                        binMerge.LoadTextures(packages, Config.p1pspConfig.texturesPath);
                    else
                        ParallelLogger.Log("[ERROR] Please set up Textures Path in config to copy over textures");
                }
                if (CreateIso)
                {
                    ParallelLogger.Log("[INFO] ISO creation is not yet supported on this platform.");
                }
            }
            else
            {
                // Standard build pipeline for all other games
                var path = GetOutputPath(game);
                Directory.CreateDirectory(path);

                // FlowMerger/BmdMerger/PM1Merger — cross-platform CLI-based script merging
                string? scriptLang = game.Contains("Persona 5 Royal") 
                    ? (game == "Persona 5 Royal (Switch)" ? Config.p5rSwitchConfig?.language : Config.p5rConfig?.language)
                    : null;

                await Task.Run(() =>
                {
                    FileMerging.FlowMerger.Merge(packages, game, scriptLang);
                    FileMerging.BmdMerger.Merge(packages, game, scriptLang);
                    FileMerging.PM1Merger.Merge(packages, game, scriptLang);
                });

                await Task.Run(() =>
                {
                    binMerge.Restart(path, EmptySND, game, CpkLang ?? "", CheatsPath, CheatsWSPath);
                    AwbMerger.Merge(packages, game, path);
                    binMerge.Unpack(packages, path, UseCpk, CpkLang ?? "", game);

                    if (packages.Exists(x => Directory.Exists(Path.Combine(x, "binarypatches"))))
                        BinaryPatcher.Patch(packages, path, UseCpk, CpkLang ?? "", game);
                    if (packages.Exists(x => Directory.Exists(Path.Combine(x, "spdpatches"))))
                        SpdPatcher.Patch(packages, path, UseCpk, CpkLang ?? "", game);

                    binMerge.Merge(path, game);
                });

                // TBL patching
                if (packages.Exists(x => Directory.Exists(Path.Combine(x, "tblpatches"))))
                {
                    var tblLang = CpkLang ?? "";
                    if (game == "Persona 5 Royal (Switch)")
                    {
                        tblLang = (Config.p5rSwitchConfig?.language ?? "English") switch
                        {
                            "English" => "EN",
                            "Spanish" => "ES",
                            "French" => "FR",
                            "German" => "DE",
                            "Italian" => "IT",
                            _ => "EN"
                        };
                    }
                    tblPatch.Patch(packages, path, UseCpk, tblLang, game);
                }

                // Game-specific post-processing
                if (game == "Persona 3 FES")
                {
                    if (packages.Exists(x => Directory.Exists(Path.Combine(x, "cheats"))))
                    {
                        if (CheatsPath != null && Directory.Exists(CheatsPath))
                            binMerge.LoadCheats(packages, CheatsPath);
                        else
                            ParallelLogger.Log("[ERROR] Please set up Cheats Path in config to copy over cheats");
                    }
                    if (packages.Exists(x => Directory.Exists(Path.Combine(x, "cheats_ws"))))
                    {
                        if (CheatsWSPath != null && Directory.Exists(CheatsWSPath))
                            binMerge.LoadCheatsWS(packages, CheatsWSPath);
                        else
                            ParallelLogger.Log("[ERROR] Please set up Cheats WS Path in config to copy over cheats_ws");
                    }
                    if (packages.Exists(x => Directory.Exists(Path.Combine(x, "texture_override"))))
                    {
                        if (TexturesPath != null && Directory.Exists(TexturesPath))
                            binMerge.LoadTextures(packages, TexturesPath);
                        else
                            ParallelLogger.Log("[ERROR] Please set up Textures Path in config to copy over textures");
                    }
                }

                if (game == "Persona 3 Portable")
                {
                    if (packages.Exists(x => Directory.Exists(Path.Combine(x, "texture_override"))))
                    {
                        if (Config.p3pConfig?.texturesPath != null && Directory.Exists(Config.p3pConfig.texturesPath))
                            binMerge.LoadTextures(packages, Config.p3pConfig.texturesPath);
                        else
                            ParallelLogger.Log("[ERROR] Please set up Textures Path in config to copy over textures");
                    }
                    binMerge.LoadFMVs(packages, Config.p3pConfig?.modDir ?? ModPath);
                    if (packages.Exists(x => Directory.Exists(Path.Combine(x, "cheats"))))
                    {
                        if (Config.p3pConfig?.cheatsPath != null)
                            binMerge.LoadP3PCheats(packages, Config.p3pConfig.cheatsPath);
                        else
                        {
                            var modDir = new DirectoryInfo(Config.p3pConfig?.modDir ?? ModPath);
                            var cheatsDir = modDir.Parent?.EnumerateDirectories("Cheats").FirstOrDefault();
                            var cheatIni = cheatsDir?.EnumerateFiles("*.ini")
                                .FirstOrDefault(f => f.Name == "ULUS10512.ini" || f.Name == "ULES01523.ini");
                            if (cheatIni != null)
                                binMerge.LoadP3PCheats(packages, cheatIni.FullName);
                            else
                                ParallelLogger.Log("[ERROR] Unable to automatically determine cheats path, please set up cheats path in config");
                        }
                    }
                }

                if (game == "Persona 4 Golden" && packages.Exists(x => Directory.Exists(Path.Combine(x, "preappfile"))))
                {
                    PreappfileAppend.Append(Path.GetDirectoryName(path)!, CpkLang ?? "");
                    PreappfileAppend.Validate(Path.GetDirectoryName(path)!, CpkLang ?? "");
                }

                // CPK creation for games that need it
                if (game == "Persona 5"
                    || (game == "Persona 5 Royal (PS4)" && Config.p5rConfig?.cpkName != "bind")
                    || game == "Persona 5 Royal (Switch)"
                    || (game == "Persona 3 Portable" && Config.p3pConfig?.cpkName != "bind")
                    || game == "Persona 4 Golden (Vita)"
                    || game == "Persona Q2" || game == "Persona Q")
                {
                    binMerge.MakeCpk(path, true);
                    if (!File.Exists($"{path}.cpk"))
                        ParallelLogger.Log($"[ERROR] Failed to build {path}.cpk!");
                }

                if (game == "Persona 4 Golden" && File.Exists(Path.Combine(ModPath, "patches", "BGME_Base.patch"))
                    && File.Exists(Path.Combine(ModPath, "patches", "BGME_Main.patch")))
                    ParallelLogger.Log("[WARNING] BGME_Base.patch and BGME_Main.patch found in your patches folder which will result in no music in battles.");
            }

            // P3F uppercase
            if (game == "Persona 3 FES")
                global::AemulusModManager.Utilities.KT.Merger.UpperAll(GetOutputPath(game));

            // Restore backup files from script merging
            FileMerging.ScriptCompiler.RestoreBackups(packages);

            buildTimer.Stop();
            ParallelLogger.Log($"[INFO] Finished Building in {Math.Round((double)buildTimer.ElapsedMilliseconds / 1000, 2)}s!");

            if (BuildFinished && _dialogService != null)
                await _dialogService.ShowNotification("Finished Building!");
        }
        catch (Exception ex)
        {
            AppendConsole($"[ERROR] Build failed: {ex.Message}");
            ParallelLogger.Log($"[ERROR] {ex.StackTrace}");
        }
        finally
        {
            IsBuildEnabled = true;
            IsUiEnabled = true;
        }
    }

    private string GetOutputPath(string game)
    {
        var modPath = ModPath!;
        return game switch
        {
            "Persona 5" => Path.Combine(modPath, Config.p5Config?.CpkName ?? "mod"),
            "Persona 3 Portable" => Path.Combine(modPath, (Config.p3pConfig?.cpkName ?? "data.cpk").Replace(".cpk", "")),
            "Persona 4 Golden (Vita)" => Path.Combine(modPath, (Config.p4gVitaConfig?.cpkName ?? "data.cpk").Replace(".cpk", "")),
            "Persona Q2" or "Persona Q" => Path.Combine(modPath, "mod"),
            "Persona 5 Royal (PS4)" => Path.Combine(modPath, (Config.p5rConfig?.cpkName ?? "bind").Replace(".cpk", "") + GetP5RLanguageSuffix()),
            "Persona 5 Royal (Switch)" => Path.Combine(modPath, "mods", "romfs", "CPK", "PATCH1"),
            _ => modPath,
        };
    }

    private string GetP5RLanguageSuffix()
    {
        return (Config.p5rConfig?.language ?? "English") switch
        {
            "French" => "_F",
            "Italian" => "_I",
            "German" => "_G",
            "Spanish" => "_S",
            _ => "",
        };
    }

    private async Task<bool> EnsureBaseFilesExist()
    {
        var game = SelectedGame;
        var originalDir = Path.Combine(_dataDir, "Original", game);

        bool needsUnpack;
        if (game == "Persona 5 Strikers")
            needsUnpack = !Directory.Exists(Path.Combine(originalDir, "motor_rsc"));
        else if (game == "Persona 1 (PSP)")
            needsUnpack = !Directory.Exists(originalDir)
                || !Directory.EnumerateFiles(originalDir, "*.bin", SearchOption.AllDirectories).Any();
        else
            needsUnpack = !Directory.Exists(originalDir)
                || !Directory.EnumerateFiles(originalDir, "*.bf", SearchOption.AllDirectories).Any();

        if (needsUnpack)
        {
            ParallelLogger.Log($"[WARNING] Base files not found for {game}. Attempting to unpack...");
            try
            {
                await UnpackBaseFiles();
            }
            catch (Exception ex)
            {
                AppendConsole($"[ERROR] Failed to unpack base files: {ex.Message}");
                return false;
            }

            // Re-check after unpack
            if (game == "Persona 5 Strikers")
                needsUnpack = !Directory.Exists(Path.Combine(originalDir, "motor_rsc"));
            else if (game == "Persona 1 (PSP)")
                needsUnpack = !Directory.Exists(originalDir)
                    || !Directory.EnumerateFiles(originalDir, "*.bin", SearchOption.AllDirectories).Any();
            else
                needsUnpack = !Directory.Exists(originalDir)
                    || !Directory.EnumerateFiles(originalDir, "*.bf", SearchOption.AllDirectories).Any();

            if (needsUnpack)
            {
                AppendConsole("[ERROR] Base files still missing after unpack. Please set up game paths in config.");
                return false;
            }
        }
        return true;
    }

    private void DeleteOldBuildOutputs(string game)
    {
        try
        {
            var modPath = ModPath!;
            switch (game)
            {
                case "Persona 5":
                {
                    var cpk = Path.Combine(modPath, (Config.p5Config?.CpkName ?? "mod.cpk") + ".cpk");
                    if (File.Exists(cpk)) File.Delete(cpk);
                    break;
                }
                case "Persona 3 Portable":
                {
                    var cpkName = Config.p3pConfig?.cpkName ?? "data.cpk";
                    var cpk = Path.Combine(modPath, cpkName);
                    if (!cpkName.EndsWith(".cpk")) cpk += ".cpk";
                    if (File.Exists(cpk)) File.Delete(cpk);
                    var fmvDir = Path.Combine(modPath, "FMV");
                    if (Directory.Exists(fmvDir)) Directory.Delete(fmvDir, true);
                    break;
                }
                case "Persona 1 (PSP)":
                {
                    var iso = Path.Combine(modPath, "P1PSP.iso");
                    if (File.Exists(iso)) File.Delete(iso);
                    break;
                }
                case "Persona 4 Golden (Vita)":
                {
                    var cpkName = Config.p4gVitaConfig?.cpkName ?? "data.cpk";
                    var cpk = Path.Combine(modPath, cpkName);
                    if (!cpkName.EndsWith(".cpk")) cpk += ".cpk";
                    if (File.Exists(cpk)) File.Delete(cpk);
                    break;
                }
                case "Persona Q2" or "Persona Q":
                {
                    var cpk = Path.Combine(modPath, "mod.cpk");
                    if (File.Exists(cpk)) File.Delete(cpk);
                    break;
                }
                case "Persona 5 Royal (PS4)":
                {
                    var cpkBase = (Config.p5rConfig?.cpkName ?? "bind").Replace(".cpk", "");
                    var cpk = Path.Combine(modPath, cpkBase + GetP5RLanguageSuffix() + ".cpk");
                    if (File.Exists(cpk)) File.Delete(cpk);
                    break;
                }
                case "Persona 5 Royal (Switch)":
                {
                    var cpk = Path.Combine(modPath, "mods", "romfs", "CPK", "PATCH1.CPK");
                    if (File.Exists(cpk)) File.Delete(cpk);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ParallelLogger.Log($"[WARNING] Failed to clean old build outputs: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LaunchGame()
    {
        var game = SelectedGame;

        if (game == "Persona 5 Strikers")
        {
            // P5S launches via Steam
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "steam://rungameid/1382330/option0",
                    UseShellExecute = true
                });
                ParallelLogger.Log("[INFO] Launched Persona 5 Strikers via Steam");
            }
            catch (Exception ex)
            {
                AppendConsole($"[ERROR] Failed to launch: {ex.Message}");
            }
            return;
        }

        if (string.IsNullOrEmpty(LauncherPath) || !File.Exists(LauncherPath))
        {
            AppendConsole("[ERROR] Launcher path not set or doesn't exist. Please configure it in Config.");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = LauncherPath,
                UseShellExecute = true
            };

            switch (game)
            {
                case "Persona 4 Golden":
                    if (!string.IsNullOrEmpty(GamePath) && File.Exists(GamePath))
                        psi.Arguments = $"--launch \"{GamePath}\"";
                    break;
                case "Persona 3 FES":
                    if (!string.IsNullOrEmpty(GamePath))
                    {
                        var elf = !string.IsNullOrEmpty(ElfPath) ? ElfPath : null;
                        // Detect PCSX2 version by launcher name
                        var launcherName = Path.GetFileNameWithoutExtension(LauncherPath).ToLowerInvariant();
                        if (launcherName.Contains("pcsx2"))
                        {
                            if (elf != null)
                                psi.Arguments = $"--nogui --elf=\"{elf}\" \"{GamePath}\"";
                            else
                                psi.Arguments = $"--nogui \"{GamePath}\"";
                        }
                        else
                        {
                            if (elf != null)
                                psi.Arguments = $"-nogui -elf \"{elf}\" -fastboot -- \"{GamePath}\"";
                            else
                                psi.Arguments = $"-nogui -fastboot -- \"{GamePath}\"";
                        }
                    }
                    break;
                case "Persona 5":
                    if (!string.IsNullOrEmpty(GamePath))
                        psi.Arguments = $"--no-gui \"{GamePath}\"";
                    break;
                case "Persona 1 (PSP)":
                    if (CreateIso)
                    {
                        var iso = Path.Combine(ModPath ?? "", "P1PSP.iso");
                        if (File.Exists(iso))
                            psi.Arguments = $"\"{iso}\"";
                    }
                    else if (!string.IsNullOrEmpty(ModPath))
                    {
                        var p1Dir = Path.Combine(ModPath, "Persona 1 (PSP)");
                        if (Directory.Exists(p1Dir))
                            psi.Arguments = $"\"{p1Dir}\"";
                    }
                    break;
                case "Persona 3 Portable":
                case "Persona Q2":
                case "Persona Q":
                case "Persona 5 Royal (Switch)":
                    if (!string.IsNullOrEmpty(GamePath))
                        psi.Arguments = $"\"{GamePath}\"";
                    break;
            }

            Process.Start(psi);
            ParallelLogger.Log($"[INFO] Launched {game}");
        }
        catch (Exception ex)
        {
            AppendConsole($"[ERROR] Failed to launch: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        AppendConsole("[INFO] Refreshing package list...");
        RefreshPackageList();
        AppendConsole("[INFO] Package list refreshed");
    }

    [RelayCommand]
    private void OpenModDir()
    {
        if (!string.IsNullOrEmpty(ModPath) && Directory.Exists(ModPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ModPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppendConsole($"[ERROR] Failed to open directory: {ex.Message}");
            }
        }
        else
        {
            AppendConsole("[WARNING] No output folder configured");
        }
    }

    [RelayCommand]
    private void OpenConfig()
    {
        // This will be called from the view which can show the ConfigWindow dialog
        AppendConsole("[INFO] Config opened");
    }

    public ConfigWindowViewModel CreateConfigViewModel()
    {
        var vm = new ConfigWindowViewModel
        {
            GameTitle = SelectedGame,
            OutputFolder = ModPath ?? "",
            ExePath = GamePath ?? "",
            LauncherPath = LauncherPath ?? "",
            ElfPath = ElfPath ?? "",
            CheatsPath = CheatsPath ?? "",
            CheatsWSPath = CheatsWSPath ?? "",
            TexturesPath = TexturesPath ?? "",
            CpkName = CpkName ?? "",
            EmptySND = EmptySND,
            UseCpk = UseCpk,
            CreateIso = CreateIso,
            AdvancedLaunchOptions = AdvancedLaunchOptions,
            BuildFinished = BuildFinished,
            BuildWarning = BuildWarning,
            UpdateChangelog = UpdateChangelog,
            DeleteOldVersions = DeleteOldVersions,
            UpdatesEnabled = UpdatesEnabled,
            UpdateAll = UpdateAll,
            ShowUnpack = SelectedGame != "Persona 5 Strikers",
        };

        switch (SelectedGame)
        {
            case "Persona 1 (PSP)":
                vm.ShowExePath = true;
                vm.ExeLabel = "P1PSP ISO Path";
                vm.ShowLauncherPath = true;
                vm.LauncherLabel = "PPSSPP Path";
                vm.ShowCheatsPath = true;
                vm.CheatsLabel = "Cheats Path";
                vm.ShowTexturesPath = true;
                vm.TexturesLabel = "Textures Folder";
                vm.ShowCreateIso = true;
                break;
            case "Persona 3 FES":
                vm.ShowExePath = true;
                vm.ExeLabel = "P3F ISO Path";
                vm.ShowElfPath = true;
                vm.ElfLabel = "P3F ELF Path";
                vm.ShowLauncherPath = true;
                vm.LauncherLabel = "PCSX2 Path";
                vm.ShowCheatsPath = true;
                vm.CheatsLabel = "Cheats Folder";
                vm.ShowCheatsWSPath = true;
                vm.CheatsWSLabel = "Cheats WS Folder";
                vm.ShowTexturesPath = true;
                vm.TexturesLabel = "Textures Folder";
                vm.ShowAdvancedLaunch = true;
                break;
            case "Persona 3 Portable":
                vm.ShowExePath = true;
                vm.ExeLabel = "P3P ISO Path";
                vm.ShowLauncherPath = true;
                vm.LauncherLabel = "PPSSPP Path";
                vm.ShowCheatsPath = true;
                vm.CheatsLabel = "Cheats Path";
                vm.ShowTexturesPath = true;
                vm.TexturesLabel = "Textures Folder";
                vm.ShowCpkFormat = true;
                vm.CpkFormats = new() { "bind", "mod.cpk", "mod1.cpk", "mod2.cpk", "mod3.cpk" };
                vm.CpkFormatIndex = vm.CpkFormats.IndexOf(CpkName ?? "mod.cpk");
                if (vm.CpkFormatIndex < 0) vm.CpkFormatIndex = 1;
                break;
            case "Persona 4 Golden":
                vm.ShowExePath = true;
                vm.ExeLabel = "P4G.exe Path";
                vm.ShowLauncherPath = true;
                vm.LauncherLabel = "Reloaded-II Path";
                vm.ShowEmptySND = true;
                vm.ShowUseCpk = true;
                vm.ShowLanguage = true;
                vm.Languages = new() { "English", "Japanese", "Chinese", "Korean" };
                vm.LanguageIndex = CpkLang switch
                {
                    "data.cpk" => 1,
                    "data_c.cpk" => 2,
                    "data_k.cpk" => 3,
                    _ => 0
                };
                break;
            case "Persona 4 Golden (Vita)":
                vm.ShowCpkFormat = true;
                vm.CpkFormats = new() { "mod.cpk", "m0.cpk", "m1.cpk", "m2.cpk", "m3.cpk" };
                vm.CpkFormatIndex = vm.CpkFormats.IndexOf(CpkName ?? "m0.cpk");
                if (vm.CpkFormatIndex < 0) vm.CpkFormatIndex = 1;
                break;
            case "Persona 5":
                vm.ShowExePath = true;
                vm.ExeLabel = "P5 EBOOT Path";
                vm.ShowLauncherPath = true;
                vm.LauncherLabel = "RPCS3 Path";
                vm.ShowCpkName = true;
                vm.CpkName = CpkName ?? "mod";
                break;
            case "Persona 5 Royal (PS4)":
                vm.ShowCpkFormat = true;
                vm.CpkFormats = new() { "bind", "mod.cpk", "mod1.cpk", "mod2.cpk", "mod3.cpk" };
                vm.CpkFormatIndex = vm.CpkFormats.IndexOf(CpkName ?? "mod.cpk");
                if (vm.CpkFormatIndex < 0) vm.CpkFormatIndex = 1;
                vm.ShowLanguage = true;
                vm.Languages = new() { "English", "French", "Italian", "German", "Spanish" };
                vm.LanguageIndex = (CpkLang ?? "English") switch
                {
                    "French" => 1,
                    "Italian" => 2,
                    "German" => 3,
                    "Spanish" => 4,
                    _ => 0
                };
                vm.ShowVersion = true;
                vm.Versions = new() { ">= 1.02", "< 1.02" };
                vm.VersionIndex = GameVersion == "< 1.02" ? 1 : 0;
                break;
            case "Persona 5 Royal (Switch)":
                vm.ShowExePath = true;
                vm.ExeLabel = "P5R ROM Path";
                vm.ShowLauncherPath = true;
                vm.LauncherLabel = "Emulator Path";
                vm.ShowLanguage = true;
                vm.Languages = new() { "English", "French", "Italian", "German", "Spanish" };
                vm.LanguageIndex = (CpkLang ?? "English") switch
                {
                    "French" => 1,
                    "Italian" => 2,
                    "German" => 3,
                    "Spanish" => 4,
                    _ => 0
                };
                break;
            case "Persona 5 Strikers":
                // Only output folder + standard checkboxes
                break;
            case "Persona Q":
                vm.ShowExePath = true;
                vm.ExeLabel = "PQ ROM Path";
                vm.ShowLauncherPath = true;
                vm.LauncherLabel = "Citra Path";
                break;
            case "Persona Q2":
                vm.ShowExePath = true;
                vm.ExeLabel = "PQ2 ROM Path";
                vm.ShowLauncherPath = true;
                vm.LauncherLabel = "Citra Path";
                break;
        }

        return vm;
    }

    public void ApplyConfigChanges(ConfigWindowViewModel configVm)
    {
        ModPath = configVm.OutputFolder;
        GamePath = configVm.ExePath;
        LauncherPath = configVm.LauncherPath;
        ElfPath = configVm.ElfPath;
        CheatsPath = configVm.CheatsPath;
        CheatsWSPath = configVm.CheatsWSPath;
        TexturesPath = configVm.TexturesPath;
        EmptySND = configVm.EmptySND;
        UseCpk = configVm.UseCpk;
        CreateIso = configVm.CreateIso;
        AdvancedLaunchOptions = configVm.AdvancedLaunchOptions;
        BuildFinished = configVm.BuildFinished;
        BuildWarning = configVm.BuildWarning;
        UpdateChangelog = configVm.UpdateChangelog;
        DeleteOldVersions = configVm.DeleteOldVersions;
        UpdatesEnabled = configVm.UpdatesEnabled;
        UpdateAll = configVm.UpdateAll;
        IsBuildEnabled = !string.IsNullOrEmpty(ModPath);

        switch (SelectedGame)
        {
            case "Persona 1 (PSP)":
                Config.p1pspConfig ??= new ConfigP1PSP();
                Config.p1pspConfig.modDir = ModPath;
                Config.p1pspConfig.isoPath = GamePath;
                Config.p1pspConfig.launcherPath = LauncherPath;
                Config.p1pspConfig.cheatsPath = CheatsPath;
                Config.p1pspConfig.texturesPath = TexturesPath;
                Config.p1pspConfig.createIso = CreateIso;
                SaveCommonConfig(Config.p1pspConfig);
                break;
            case "Persona 3 FES":
                Config.p3fConfig ??= new ConfigP3F();
                Config.p3fConfig.modDir = ModPath;
                Config.p3fConfig.isoPath = GamePath;
                Config.p3fConfig.elfPath = ElfPath;
                Config.p3fConfig.launcherPath = LauncherPath;
                Config.p3fConfig.cheatsPath = CheatsPath;
                Config.p3fConfig.cheatsWSPath = CheatsWSPath;
                Config.p3fConfig.texturesPath = TexturesPath;
                Config.p3fConfig.advancedLaunchOptions = AdvancedLaunchOptions;
                Config.p3fConfig.buildFinished = BuildFinished;
                Config.p3fConfig.buildWarning = BuildWarning;
                Config.p3fConfig.updateChangelog = UpdateChangelog;
                Config.p3fConfig.deleteOldVersions = DeleteOldVersions;
                Config.p3fConfig.updatesEnabled = UpdatesEnabled;
                Config.p3fConfig.updateAll = UpdateAll;
                break;
            case "Persona 3 Portable":
                Config.p3pConfig ??= new ConfigP3P();
                Config.p3pConfig.modDir = ModPath;
                Config.p3pConfig.isoPath = GamePath;
                Config.p3pConfig.launcherPath = LauncherPath;
                Config.p3pConfig.cheatsPath = CheatsPath;
                Config.p3pConfig.texturesPath = TexturesPath;
                Config.p3pConfig.cpkName = configVm.CpkFormats.Count > 0 ? configVm.CpkFormats[configVm.CpkFormatIndex] : "mod.cpk";
                CpkName = Config.p3pConfig.cpkName;
                SaveCommonConfig(Config.p3pConfig);
                break;
            case "Persona 4 Golden":
                Config.p4gConfig ??= new ConfigP4G();
                Config.p4gConfig.modDir = ModPath;
                Config.p4gConfig.exePath = GamePath;
                Config.p4gConfig.reloadedPath = LauncherPath;
                Config.p4gConfig.emptySND = EmptySND;
                Config.p4gConfig.useCpk = UseCpk;
                Config.p4gConfig.cpkLang = configVm.LanguageIndex switch
                {
                    1 => "data.cpk",
                    2 => "data_c.cpk",
                    3 => "data_k.cpk",
                    _ => "data_e.cpk"
                };
                CpkLang = Config.p4gConfig.cpkLang;
                SaveCommonConfig(Config.p4gConfig);
                break;
            case "Persona 4 Golden (Vita)":
                Config.p4gVitaConfig ??= new ConfigP4GVita();
                Config.p4gVitaConfig.modDir = ModPath;
                Config.p4gVitaConfig.cpkName = configVm.CpkFormats.Count > 0 ? configVm.CpkFormats[configVm.CpkFormatIndex] : "m0.cpk";
                CpkName = Config.p4gVitaConfig.cpkName;
                SaveCommonConfig(Config.p4gVitaConfig);
                break;
            case "Persona 5":
                Config.p5Config ??= new ConfigP5();
                Config.p5Config.modDir = ModPath;
                Config.p5Config.gamePath = GamePath;
                Config.p5Config.launcherPath = LauncherPath;
                Config.p5Config.CpkName = configVm.CpkName;
                CpkName = Config.p5Config.CpkName;
                SaveCommonConfig(Config.p5Config);
                break;
            case "Persona 5 Royal (PS4)":
                Config.p5rConfig ??= new ConfigP5R();
                Config.p5rConfig.modDir = ModPath;
                Config.p5rConfig.cpkName = configVm.CpkFormats.Count > 0 ? configVm.CpkFormats[configVm.CpkFormatIndex] : "mod.cpk";
                CpkName = Config.p5rConfig.cpkName;
                Config.p5rConfig.language = configVm.Languages.Count > 0 ? configVm.Languages[configVm.LanguageIndex] : "English";
                CpkLang = Config.p5rConfig.language;
                Config.p5rConfig.version = configVm.Versions.Count > 0 ? configVm.Versions[configVm.VersionIndex] : ">= 1.02";
                GameVersion = Config.p5rConfig.version;
                SaveCommonConfig(Config.p5rConfig);
                break;
            case "Persona 5 Royal (Switch)":
                Config.p5rSwitchConfig ??= new ConfigP5RSwitch();
                Config.p5rSwitchConfig.modDir = ModPath;
                Config.p5rSwitchConfig.gamePath = GamePath;
                Config.p5rSwitchConfig.launcherPath = LauncherPath;
                Config.p5rSwitchConfig.language = configVm.Languages.Count > 0 ? configVm.Languages[configVm.LanguageIndex] : "English";
                CpkLang = Config.p5rSwitchConfig.language;
                SaveCommonConfig(Config.p5rSwitchConfig);
                break;
            case "Persona 5 Strikers":
                Config.p5sConfig ??= new ConfigP5S();
                Config.p5sConfig.modDir = ModPath;
                SaveCommonConfig(Config.p5sConfig);
                break;
            case "Persona Q":
                Config.pqConfig ??= new ConfigPQ();
                Config.pqConfig.modDir = ModPath;
                Config.pqConfig.ROMPath = GamePath;
                Config.pqConfig.launcherPath = LauncherPath;
                SaveCommonConfig(Config.pqConfig);
                break;
            case "Persona Q2":
                Config.pq2Config ??= new ConfigPQ2();
                Config.pq2Config.modDir = ModPath;
                Config.pq2Config.ROMPath = GamePath;
                Config.pq2Config.launcherPath = LauncherPath;
                SaveCommonConfig(Config.pq2Config);
                break;
        }

        UpdateConfig();
        AppendConsole("[INFO] Config saved");
    }

    private void SaveCommonConfig(dynamic config)
    {
        config.buildFinished = BuildFinished;
        config.buildWarning = BuildWarning;
        config.updateChangelog = UpdateChangelog;
        config.deleteOldVersions = DeleteOldVersions;
        config.updatesEnabled = UpdatesEnabled;
        config.updateAll = UpdateAll;
    }

    public async Task UnpackBaseFiles()
    {
        AppendConsole($"[INFO] Unpacking base files for {SelectedGame}...");
        IsBuildEnabled = false;
        try
        {
            await Task.Run(async () =>
            {
                switch (SelectedGame)
                {
                    case "Persona 1 (PSP)":
                        if (string.IsNullOrEmpty(GamePath))
                        { AppendConsole("[ERROR] ISO path not set."); return; }
                        await PacUnpacker.UnzipAndUnBin(GamePath);
                        break;
                    case "Persona 3 FES":
                        if (string.IsNullOrEmpty(GamePath))
                        { AppendConsole("[ERROR] ISO path not set."); return; }
                        await PacUnpacker.Unzip(GamePath);
                        break;
                    case "Persona 3 Portable":
                        if (string.IsNullOrEmpty(GamePath))
                        { AppendConsole("[ERROR] ISO path not set."); return; }
                        await PacUnpacker.UnzipAndUnpackCPK(GamePath);
                        break;
                    case "Persona 4 Golden":
                        if (string.IsNullOrEmpty(GamePath))
                        { AppendConsole("[ERROR] Game path not set."); return; }
                        PacUnpacker.Unpack(GamePath, CpkLang ?? "data_e.cpk");
                        break;
                    case "Persona 4 Golden (Vita)":
                        if (string.IsNullOrEmpty(ModPath))
                        { AppendConsole("[ERROR] Output path not set."); return; }
                        await PacUnpacker.UnpackP4GCPK(ModPath);
                        break;
                    case "Persona 5":
                        if (string.IsNullOrEmpty(GamePath))
                        { AppendConsole("[ERROR] Game path not set."); return; }
                        await PacUnpacker.UnpackP5CPK(GamePath);
                        break;
                    case "Persona 5 Royal (PS4)":
                        if (string.IsNullOrEmpty(ModPath))
                        { AppendConsole("[ERROR] Output path not set."); return; }
                        await PacUnpacker.UnpackP5RCPKs(ModPath, CpkLang ?? "English", GameVersion ?? ">= 1.02");
                        break;
                    case "Persona 5 Royal (Switch)":
                        if (string.IsNullOrEmpty(GamePath))
                        { AppendConsole("[ERROR] ROM path not set."); return; }
                        await PacUnpacker.UnpackP5RSwitchCPKs(GamePath, CpkLang ?? "English");
                        break;
                    case "Persona 5 Strikers":
                        if (string.IsNullOrEmpty(ModPath))
                        { AppendConsole("[ERROR] Output path not set."); return; }
                        // Strikers just backs up
                        global::AemulusModManager.Utilities.KT.Merger.Backup(ModPath);
                        break;
                    case "Persona Q":
                        if (string.IsNullOrEmpty(GamePath))
                        { AppendConsole("[ERROR] ROM path not set."); return; }
                        await PacUnpacker.UnpackPQCPK(GamePath);
                        break;
                    case "Persona Q2":
                        if (string.IsNullOrEmpty(GamePath))
                        { AppendConsole("[ERROR] ROM path not set."); return; }
                        await PacUnpacker.UnpackPQ2CPK(GamePath);
                        break;
                }
            });
            AppendConsole("[INFO] Unpack finished!");
        }
        catch (Exception ex)
        {
            AppendConsole($"[ERROR] Unpack failed: {ex.Message}");
        }
        finally
        {
            IsBuildEnabled = true;
        }
    }

    #endregion

    #region Context Menu / Package Operations

    /// <summary>
    /// Opens a file/folder picker to add packages. Used as the primary install method
    /// (drag-drop doesn't work on Wayland).
    /// </summary>
    public async Task AddPackagesFromPicker(Window owner)
    {
        var sp = owner.StorageProvider;

        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select package archives to install",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Archives") { Patterns = new[] { "*.7z", "*.zip", "*.rar" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count == 0) return;

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        if (paths.Length == 0) return;

        IsUiEnabled = false;
        try
        {
            await ExtractPackages(paths!);
        }
        finally
        {
            IsUiEnabled = true;
        }
    }

    public async Task CreateNewPackage(Window owner)
    {
        var window = new Views.CreatePackageWindow();
        await window.ShowDialog(owner);

        if (window.ResultMetadata != null)
        {
            try
            {
                string dirName = !string.IsNullOrEmpty(window.ResultMetadata.version)
                    ? $"{window.ResultMetadata.name} {window.ResultMetadata.version}"
                    : window.ResultMetadata.name;
                dirName = string.Join("_", dirName.Split(Path.GetInvalidFileNameChars()));

                var pkgDir = Path.Combine(_dataDir, "Packages", SelectedGame, dirName);
                Directory.CreateDirectory(pkgDir);

                var xmlPath = Path.Combine(pkgDir, "Package.xml");
                using (var stream = File.Create(xmlPath))
                    _xsp.Serialize(stream, window.ResultMetadata);

                if (!string.IsNullOrEmpty(window.ThumbnailPath) && File.Exists(window.ThumbnailPath))
                {
                    var ext = Path.GetExtension(window.ThumbnailPath).ToLower();
                    File.Copy(window.ThumbnailPath, Path.Combine(pkgDir, $"Preview{ext}"), true);
                }

                AppendConsole($"[INFO] Created new package: {dirName}");
            }
            catch (Exception ex)
            {
                AppendConsole($"[ERROR] Failed to create package: {ex.Message}");
            }

            RefreshPackageList();
            SavePackages();
        }
    }

    [RelayCommand]
    private void OpenPackageFolder()
    {
        if (SelectedPackage == null || string.IsNullOrEmpty(SelectedPackage.path)) return;
        var path = Path.Combine(_dataDir, "Packages", SelectedGame, SelectedPackage.path);
        if (!Directory.Exists(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            AppendConsole($"[INFO] Opened Packages/{SelectedGame}/{SelectedPackage.path}");
        }
        catch (Exception ex)
        {
            AppendConsole($"[ERROR] Couldn't open folder: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task EditPackageMetadata()
    {
        if (SelectedPackage == null || string.IsNullOrEmpty(SelectedPackage.path)) return;
        if (_dialogService == null) return;

        var xmlPath = Path.Combine(_dataDir, "Packages", SelectedGame, SelectedPackage.path, "Package.xml");
        if (!File.Exists(xmlPath))
        {
            AppendConsole($"[WARNING] No Package.xml found for {SelectedPackage.path}");
            return;
        }

        Metadata m = new()
        {
            name = SelectedPackage.name,
            author = SelectedPackage.author,
            id = SelectedPackage.id,
            version = SelectedPackage.version,
            link = SelectedPackage.link,
            description = SelectedPackage.description,
            skippedVersion = SelectedPackage.skippedVersion
        };

        var window = new Views.CreatePackageWindow(m);
        await window.ShowDialog(_dialogService.OwnerWindow);

        if (window.ResultMetadata != null)
        {
            try
            {
                using var stream = File.Create(xmlPath);
                _xsp.Serialize(stream, window.ResultMetadata);

                if (!string.IsNullOrEmpty(window.ThumbnailPath) && File.Exists(window.ThumbnailPath))
                {
                    var pkgDir = Path.Combine(_dataDir, "Packages", SelectedGame, SelectedPackage.path);
                    foreach (var old in new DirectoryInfo(pkgDir).GetFiles("Preview.*"))
                        old.Delete();
                    var ext = Path.GetExtension(window.ThumbnailPath).ToLower();
                    File.Copy(window.ThumbnailPath, Path.Combine(pkgDir, $"Preview{ext}"), true);
                }
            }
            catch (Exception ex)
            {
                AppendConsole($"[ERROR] Failed to save metadata: {ex.Message}");
            }
        }

        RefreshPackageList();
        SavePackages();
    }

    [RelayCommand]
    private async Task DeletePackage()
    {
        if (SelectedPackage == null || string.IsNullOrEmpty(SelectedPackage.path)) return;
        if (_dialogService == null) return;

        var confirmed = await _dialogService.ShowConfirmation(
            $"Are you sure you want to delete Packages/{SelectedGame}/{SelectedPackage.path}?");
        if (!confirmed) return;

        var path = Path.Combine(_dataDir, "Packages", SelectedGame, SelectedPackage.path);
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, true);
                AppendConsole($"[INFO] Deleted Packages/{SelectedGame}/{SelectedPackage.path}");
            }
            catch (Exception ex)
            {
                AppendConsole($"[ERROR] Couldn't delete package: {ex.Message}");
            }
        }

        RefreshPackageList();
        SavePackages();
        SelectedPackage = null;
        Description = "";
        PreviewImage = _placeholderImage.Value;
    }

    [RelayCommand]
    private async Task ZipPackage()
    {
        if (SelectedPackage == null || string.IsNullOrEmpty(SelectedPackage.path)) return;
        if (_dialogService == null) return;

        var pkgDir = Path.Combine(_dataDir, "Packages", SelectedGame, SelectedPackage.path);
        if (!Directory.Exists(pkgDir)) return;

        var savePath = await _dialogService.ShowSaveFileDialog(
            $"{SelectedPackage.path}.7z", "7zip", "*.7z");
        if (string.IsNullOrEmpty(savePath)) return;

        await Task.Run(() =>
        {
            var sevenZip = Find7Zip();
            if (sevenZip == null)
            {
                AppendConsole("[ERROR] 7z not found");
                return;
            }

            if (File.Exists(savePath)) File.Delete(savePath);

            var psi = new ProcessStartInfo
            {
                FileName = sevenZip,
                Arguments = $"a \"{savePath}\" \"{Path.GetFileName(SelectedPackage.path)}/*\"",
                WorkingDirectory = Path.Combine(_dataDir, "Packages", SelectedGame),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            // On non-Windows, use mono if it's a .exe
            if (!OperatingSystem.IsWindows() && sevenZip.EndsWith(".exe"))
            {
                psi.Arguments = $"\"{sevenZip}\" {psi.Arguments}";
                psi.FileName = "mono";
            }

            AppendConsole($"[INFO] Zipping {SelectedPackage.path}...");
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
            AppendConsole($"[INFO] Created {savePath}");
        });
    }

    [RelayCommand]
    private void ConvertCPK()
    {
        if (SelectedPackage == null || string.IsNullOrEmpty(SelectedPackage.path)) return;
        if (SelectedGame != "Persona 4 Golden") return;

        var pkgDir = Path.Combine(_dataDir, "Packages", SelectedGame, SelectedPackage.path);
        if (!Directory.Exists(pkgDir)) return;

        var cpkBase = CpkLang != null ? Path.GetFileNameWithoutExtension(CpkLang) : "data_e";

        foreach (var folder in Directory.GetDirectories(pkgDir))
        {
            var name = Path.GetFileName(folder);
            if (name.StartsWith("data0"))
            {
                var dest = Path.Combine(pkgDir, cpkBase);
                if (!Directory.Exists(dest))
                    Directory.Move(folder, dest);
            }
            else if (name.StartsWith("movie0"))
            {
                var dest = Path.Combine(pkgDir, "movie");
                if (!Directory.Exists(dest))
                    Directory.Move(folder, dest);
            }
        }

        var modsAem = Path.Combine(pkgDir, "mods.aem");
        if (File.Exists(modsAem))
        {
            var text = File.ReadAllText(modsAem);
            text = Regex.Replace(text, "data0000[0-6]", cpkBase);
            File.WriteAllText(modsAem, text);
        }

        AppendConsole($"[INFO] Converted {SelectedPackage.path} to CPK structure");
    }

    [RelayCommand]
    private void HidePackage()
    {
        if (SelectedPackage == null) return;
        SelectedPackage.hidden = true;
        foreach (var p in _packageList.Where(p => p.path == SelectedPackage.path))
            p.hidden = true;
        SavePackages();
        AppendConsole($"[INFO] Hid {SelectedPackage.name}");
    }

    [RelayCommand]
    private void UnhidePackage()
    {
        if (SelectedPackage == null) return;
        SelectedPackage.hidden = false;
        foreach (var p in _packageList.Where(p => p.path == SelectedPackage.path))
            p.hidden = false;
        SavePackages();
        AppendConsole($"[INFO] Unhid {SelectedPackage.name}");
    }

    [RelayCommand]
    private async Task UpdateSelectedPackage()
    {
        if (SelectedPackage == null || !UpdatesEnabled || _dialogService == null) return;

        IsUiEnabled = false;
        try
        {
            _cancellationToken = new CancellationTokenSource();
            _updating = true;
            AppendConsole($"[INFO] Checking for updates for {SelectedPackage.name}...");
            var updater = new PackageUpdater(_dialogService);
            await updater.CheckForUpdate(new[] { SelectedPackage }, SelectedGame, _cancellationToken);
            _updating = false;

            var configTemp = Path.Combine(_configPath, "temp");
            if (Directory.Exists(configTemp))
                ReplacePackagesXML();

            RefreshPackageList();
            SavePackages();
            AppendConsole("[INFO] Finished checking for updates!");
        }
        finally
        {
            _updating = false;
            IsUiEnabled = true;
        }
    }

    [RelayCommand]
    private async Task UpdateAllPackages()
    {
        if (_updating || !UpdatesEnabled || _dialogService == null) return;

        IsUiEnabled = false;
        try
        {
            _updating = true;
            _cancellationToken = new CancellationTokenSource();
            AppendConsole("[INFO] Checking for updates for all applicable packages...");

            var updatableRows = DisplayedPackages
                .Where(p => !string.IsNullOrEmpty(p.link))
                .ToArray();

            var updater = new PackageUpdater(_dialogService);
            if (await updater.CheckForUpdate(updatableRows, SelectedGame, _cancellationToken))
            {
                var configTemp = Path.Combine(_configPath, "temp");
                if (Directory.Exists(configTemp))
                    ReplacePackagesXML();
                RefreshPackageList();
                SavePackages();
            }
            _updating = false;
            AppendConsole("[INFO] Finished checking for updates!");
        }
        finally
        {
            _updating = false;
            IsUiEnabled = true;
        }
    }

    public void CheckVersioning()
    {
        var game = SelectedGame;
        var packagesDir = Path.Combine(_dataDir, "Packages", game);

        var latestVersions = DisplayedPackages
            .GroupBy(t => t.id)
            .Select(g => g.OrderByDescending(t => Version.TryParse(t.version, out var v) ? v : null)
                          .ThenByDescending(t =>
                          {
                              var dir = Path.Combine(packagesDir, t.path ?? "");
                              return Directory.Exists(dir) ? new DirectoryInfo(dir).LastWriteTime : DateTime.MinValue;
                          }).First())
            .ToList();

        // Preserve enabled state
        foreach (var package in latestVersions)
        {
            if (DisplayedPackages.Where(x => x.id == package.id).Any(y => y.enabled))
                package.enabled = true;
        }

        DisplayedPackages = new ObservableCollection<DisplayedMetadata>(latestVersions);

        // Sync _packageList
        var temp = _packageList.ToList();
        temp.RemoveAll(x => !DisplayedPackages.Select(y => y.path).Contains(x.path));
        _packageList = new ObservableCollection<Package>(temp);

        // Delete old versions if configured
        if (DeleteOldVersions && Directory.Exists(packagesDir))
        {
            foreach (var package in Directory.GetDirectories(packagesDir))
            {
                if (!_packageList.Select(t => t.path).Contains(Path.GetFileName(package)))
                {
                    try
                    {
                        AppendConsole($"[INFO] Deleting old version: {Path.GetFileName(package)}...");
                        Directory.Delete(package, true);
                    }
                    catch (Exception ex)
                    {
                        AppendConsole($"[ERROR] Couldn't delete {package}: {ex.Message}");
                    }
                }
            }
        }
    }

    public async Task ExtractPackages(string[] fileList)
    {
        string? loadout = null;
        bool dropped = false;
        var game = SelectedGame;
        var packagesDir = Path.Combine(_dataDir, "Packages", game);
        Directory.CreateDirectory(packagesDir);

        foreach (var file in fileList)
        {
            if (Directory.Exists(file))
            {
                // Move directory directly into Packages
                AppendConsole($"[INFO] Moving {Path.GetFileName(file)} into Packages/{game}");
                var dest = Path.Combine(packagesDir, Path.GetFileName(file));
                int index = 2;
                while (Directory.Exists(dest))
                {
                    dest = Path.Combine(packagesDir, $"{Path.GetFileName(file)} ({index})");
                    index++;
                }
                Directory.Move(file, dest);
                dropped = true;
            }
            else if (IsArchive(file))
            {
                await ExtractArchive(file, packagesDir, game);
                dropped = true;
            }
            else if (Path.GetExtension(file).Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                // Import loadout XML
                loadout = await ImportLoadoutXml(file, game);
                if (loadout != null) dropped = true;
            }
            else
            {
                AppendConsole($"[WARNING] {file} isn't a folder, .zip, .7z, .rar, or loadout xml, skipping...");
            }
        }

        // Clean temp
        var tempPath = Path.Combine(_dataDir, "temp");
        if (Directory.Exists(tempPath))
        {
            try { Directory.Delete(tempPath, true); } catch { }
        }

        if (dropped)
        {
            RefreshPackageList();
            SavePackages();
            if (loadout != null)
            {
                var loadouts = new Loadouts(game);
                _loadoutChanging = true;
                LoadoutItems = loadouts.LoadoutItems;
                _selectedLoadout = loadout;
                _lastLoadout = loadout;
                _loadoutChanging = false;
                OnPropertyChanged(nameof(SelectedLoadout));
            }
        }
    }

    private static bool IsArchive(string file) =>
        Path.GetExtension(file).ToLower() is ".7z" or ".rar" or ".zip";

    private async Task ExtractArchive(string file, string packagesDir, string game)
    {
        var tempDir = Path.Combine(_dataDir, "temp");
        Directory.CreateDirectory(tempDir);

        var sevenZip = Find7Zip();
        if (sevenZip == null)
        {
            AppendConsole("[ERROR] 7z not found");
            return;
        }

        AppendConsole($"[INFO] Extracting {Path.GetFileName(file)} into Packages/{game}");

        await Task.Run(() =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = sevenZip,
                Arguments = $"x -y \"{file}\" -o\"{tempDir}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            if (!OperatingSystem.IsWindows() && sevenZip.EndsWith(".exe"))
            {
                psi.Arguments = $"\"{sevenZip}\" {psi.Arguments}";
                psi.FileName = "mono";
            }

            using var proc = Process.Start(psi);
            proc?.WaitForExit();
        });

        // Determine destination
        var entries = Directory.GetFileSystemEntries(tempDir);
        if (entries.Length > 1)
        {
            // Multiple items: use archive name as folder
            var dest = Path.Combine(packagesDir, Path.GetFileNameWithoutExtension(file));
            int index = 2;
            while (Directory.Exists(dest))
            {
                dest = Path.Combine(packagesDir, $"{Path.GetFileNameWithoutExtension(file)} ({index})");
                index++;
            }
            Directory.Move(tempDir, dest);
        }
        else if (entries.Length == 1 && Directory.Exists(entries[0]))
        {
            // Single folder: move it
            var dest = Path.Combine(packagesDir, Path.GetFileName(entries[0]));
            int index = 2;
            while (Directory.Exists(dest))
            {
                dest = Path.Combine(packagesDir, $"{Path.GetFileName(entries[0])} ({index})");
                index++;
            }
            Directory.Move(entries[0], dest);
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private async Task<string?> ImportLoadoutXml(string file, string game)
    {
        AppendConsole($"[INFO] Trying to import {Path.GetFileName(file)} as a loadout xml");
        try
        {
            using var stream = File.OpenRead(file);
            var packages = (Packages?)_xp.Deserialize(stream);
            if (packages?.packages == null) return null;

            var loadoutName = Path.GetFileNameWithoutExtension(file);
            var loadoutFile = Path.Combine(_configPath, game, $"{loadoutName}.xml");

            if (File.Exists(loadoutFile))
            {
                if (_dialogService != null)
                {
                    await _dialogService.ShowNotification($"A loadout named \"{loadoutName}\" already exists.");
                    var (newName, _) = await _dialogService.ShowInputDialog("Enter a new name for the loadout:");
                    if (string.IsNullOrWhiteSpace(newName)) return null;
                    loadoutName = newName;
                    loadoutFile = Path.Combine(_configPath, game, $"{loadoutName}.xml");
                }
            }

            // Copy file
            Directory.CreateDirectory(Path.GetDirectoryName(loadoutFile)!);
            File.Copy(file, loadoutFile, true);
            return loadoutName;
        }
        catch (Exception ex)
        {
            AppendConsole($"[ERROR] Invalid loadout xml: {ex.Message}");
            return null;
        }
    }

    private string? Find7Zip()
    {
        // Check for native 7z first
        var native7z = Path.Combine(_exeDir, "Dependencies", "7z", "7z");
        if (File.Exists(native7z)) return native7z;
        var exe7z = Path.Combine(_exeDir, "Dependencies", "7z", "7z.exe");
        if (File.Exists(exe7z)) return exe7z;

        // Try system PATH
        try
        {
            var psi = new ProcessStartInfo("7z", "--help")
            {
                CreateNoWindow = true, UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
            if (proc?.ExitCode == 0) return "7z";
        }
        catch { }

        return null;
    }

    [RelayCommand]
    private void ToggleDarkMode()
    {
        DarkMode = !DarkMode;
        Config.darkMode = DarkMode;
        UpdateConfig();

        // Switch Avalonia theme variant — DynamicResource ThemeDictionaries handle the rest
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = DarkMode ? ThemeVariant.Dark : ThemeVariant.Light;

        AppendConsole(DarkMode ? "[INFO] Switched to dark mode." : "[INFO] Switched to light mode.");
    }

    public void ToggleSelectedPackageEnabled()
    {
        if (SelectedPackage == null) return;
        var newState = !SelectedPackage.enabled;
        TogglePackageEnabled(SelectedPackage, newState);
    }

    #endregion

    #region Selection Changes

    partial void OnSelectedPackageChanged(DisplayedMetadata? value)
    {
        Description = value?.description ?? "";
        LoadPreviewImage(value);
    }

    private static readonly Lazy<Bitmap?> _placeholderImage = new(() =>
    {
        try
        {
            var uri = new Uri("avares://AemulusPackageManager/Assets/Preview.png");
            using var stream = global::Avalonia.Platform.AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch { return null; }
    });

    private void LoadPreviewImage(DisplayedMetadata? package)
    {
        if (package == null || string.IsNullOrEmpty(package.path))
        {
            PreviewImage = _placeholderImage.Value;
            return;
        }

        var packageDir = Path.Combine(_dataDir, "Packages", SelectedGame, package.path);

        if (!Directory.Exists(packageDir))
        {
            PreviewImage = _placeholderImage.Value;
            return;
        }

        try
        {
            var previewFiles = new DirectoryInfo(packageDir).GetFiles("Preview.*");
            if (previewFiles.Length > 0)
            {
                var bytes = File.ReadAllBytes(previewFiles[0].FullName);
                using var ms = new MemoryStream(bytes);
                PreviewImage = new Bitmap(ms);
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load preview image: {ex.Message}");
        }

        PreviewImage = _placeholderImage.Value;
    }

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            RefreshPackageList();
            return;
        }

        var search = value.ToLowerInvariant();
        var modDir = ModPath ?? "";

        var filtered = new ObservableCollection<DisplayedMetadata>();
        foreach (var p in _packageList)
        {
            var meta = new Metadata();
            var metadataFile = Path.Combine(modDir, p.path ?? "", "Package.xml");

            if (File.Exists(metadataFile))
            {
                try
                {
                    using var stream = File.OpenRead(metadataFile);
                    meta = (Metadata)_xsp.Deserialize(stream);
                }
                catch { }
            }

            if ((p.path?.ToLowerInvariant().Contains(search) ?? false) ||
                (meta.name?.ToLowerInvariant().Contains(search) ?? false) ||
                (meta.author?.ToLowerInvariant().Contains(search) ?? false))
            {
                var displayed = InitDisplayedMetadata(meta);
                displayed.enabled = p.enabled;
                displayed.path = p.path;
                filtered.Add(displayed);
            }
        }
        DisplayedPackages = filtered;
    }

    #endregion

    public void AppendConsole(string message)
    {
        ConsoleOutput += message + Environment.NewLine;
    }

    public void AppendConsoleRaw(string text)
    {
        ConsoleOutput += text;
    }

    [RelayCommand]
    private void ClearConsole()
    {
        ConsoleOutput = "";
    }

    private void UpdateGameAccentColor()
    {
        GameAccentColor = Converters.GameColorConverter.GetBrush(SelectedGame);
    }

    public async void UpdateStats()
    {
        var version = _appVersion;
        if (version.Length > 0 && version.Contains('.'))
        {
            var lastDot = version.LastIndexOf('.');
            if (lastDot > 0) version = version[..lastDot];
        }
        PackageStats = $"-- packages \u2022 -- enabled \u2022 -- files \u2022 -- Bytes \u2022 v{version}";

        var game = SelectedGame;
        var packagesDir = Path.Combine(_dataDir, "Packages", game);
        if (!Directory.Exists(packagesDir))
        {
            PackageStats = $"0 packages \u2022 0 enabled \u2022 0 files \u2022 0 Bytes \u2022 v{version}";
            return;
        }

        var packageCount = _packageList.Count;
        var enabledCount = _packageList.Count(x => x.enabled);

        await Task.Run(() =>
        {
            try
            {
                var numFiles = Directory.GetFiles(packagesDir, "*", SearchOption.AllDirectories).Length;
                var dirSize = AemulusModManager.Utilities.PackageUpdating.StringConverters.FormatSize(
                    new DirectoryInfo(packagesDir).GetDirectorySize());
                PackageStats = $"{packageCount} packages \u2022 {enabledCount} enabled \u2022 {numFiles:N0} files \u2022 {dirSize} \u2022 v{version}";
            }
            catch
            {
                PackageStats = $"{packageCount} packages \u2022 {enabledCount} enabled \u2022 v{version}";
            }
        });
    }
}
