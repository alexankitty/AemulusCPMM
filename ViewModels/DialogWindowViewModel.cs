using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AemulusModManager.Avalonia.ViewModels;

public partial class DialogWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _gameTitle = "";
    [ObservableProperty] private string _loadoutName = "";
}