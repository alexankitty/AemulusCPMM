using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AemulusModManager.Avalonia.Views;
using AemulusModManager.Avalonia.Utilities;

namespace AemulusModManager.Avalonia;

public partial class App : Application
{
    public static IPC? Ipc;
    public override void Initialize()
    {
        Ipc = new IPC();
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(desktop.Args);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
