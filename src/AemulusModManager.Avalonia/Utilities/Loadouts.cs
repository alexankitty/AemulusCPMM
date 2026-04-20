using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace AemulusModManager.Avalonia.Utilities;

public class Loadouts
{
    public ObservableCollection<string> LoadoutItems;

    public Loadouts(string game)
    {
        LoadoutItems = new ObservableCollection<string>();
        LoadLoadouts(game);
    }

    public void LoadLoadouts(string game)
    {
        string configPath = AppPaths.ConfigDir;
        AemulusModManager.Utilities.ParallelLogger.Log($"[INFO] Loading loadouts for {game}");
        Directory.CreateDirectory(Path.Combine(configPath, game));

        // If the old single loadout file existed, convert it to the new one with a name of default
        var oldPath = Path.Combine(configPath, $"{game.Replace(" ", "")}Packages.xml");
        var newPath = Path.Combine(configPath, game, "Default.xml");
        if (File.Exists(oldPath) && !File.Exists(newPath))
        {
            AemulusModManager.Utilities.ParallelLogger.Log("[INFO] Old loadout detected, converting to new one with name \"Default\"");
            File.Move(oldPath, newPath);
        }

        // Get all loadouts for the current game
        string[] loadoutFiles = Directory.GetFiles(Path.Combine(configPath, game))
            .Where(path => Path.GetExtension(path) == ".xml")
            .ToArray();

        // Create a default loadout if none exists
        if (loadoutFiles.Length == 0)
        {
            loadoutFiles = loadoutFiles.Append("Default").ToArray();
        }

        // Change the loadout items to the new ones
        LoadoutItems = new ObservableCollection<string>();
        foreach (string loadout in loadoutFiles)
        {
            LoadoutItems.Add(Path.GetFileNameWithoutExtension(loadout));
        }
        LoadoutItems.Add("Add new loadout");
    }
}
