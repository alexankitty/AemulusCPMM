using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AemulusModManager.Avalonia.ViewModels;

public partial class ConfigWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _gameTitle = "";
    [ObservableProperty] private string _outputFolder = "";
    [ObservableProperty] private string _exePath = "";
    [ObservableProperty] private string _launcherPath = "";
    [ObservableProperty] private string _cheatsPath = "";
    [ObservableProperty] private string _cheatsWSPath = "";
    [ObservableProperty] private string _texturesPath = "";
    [ObservableProperty] private string _elfPath = "";
    [ObservableProperty] private string _cpkName = "";
    [ObservableProperty] private bool _emptySND;
    [ObservableProperty] private bool _useCpk;
    [ObservableProperty] private bool _createIso;
    [ObservableProperty] private bool _advancedLaunchOptions;
    [ObservableProperty] private bool _buildFinished;
    [ObservableProperty] private bool _buildWarning;
    [ObservableProperty] private bool _updateChangelog;
    [ObservableProperty] private bool _deleteOldVersions;
    [ObservableProperty] private bool _updatesEnabled;
    [ObservableProperty] private bool _updateAll;
    [ObservableProperty] private int _languageIndex;
    [ObservableProperty] private int _cpkFormatIndex;
    [ObservableProperty] private int _versionIndex;

    // Visibility flags per-game
    [ObservableProperty] private bool _showEmptySND;
    [ObservableProperty] private bool _showUseCpk;
    [ObservableProperty] private bool _showLanguage;
    [ObservableProperty] private bool _showExePath;
    [ObservableProperty] private bool _showLauncherPath;
    [ObservableProperty] private bool _showUnpack;
    [ObservableProperty] private bool _showCheatsPath;
    [ObservableProperty] private bool _showCheatsWSPath;
    [ObservableProperty] private bool _showTexturesPath;
    [ObservableProperty] private bool _showElfPath;
    [ObservableProperty] private bool _showCpkName;
    [ObservableProperty] private bool _showCpkFormat;
    [ObservableProperty] private bool _showVersion;
    [ObservableProperty] private bool _showCreateIso;
    [ObservableProperty] private bool _showAdvancedLaunch;

    public string ExeLabel { get; set; } = "Executable Path";
    public string LauncherLabel { get; set; } = "Launcher Path";
    public string CheatsLabel { get; set; } = "Cheats Folder";
    public string CheatsWSLabel { get; set; } = "Cheats WS Folder";
    public string TexturesLabel { get; set; } = "Textures Folder";
    public string ElfLabel { get; set; } = "ELF Path";
    public List<string> Languages { get; set; } = new();
    public List<string> CpkFormats { get; set; } = new();
    public List<string> Versions { get; set; } = new();
}
