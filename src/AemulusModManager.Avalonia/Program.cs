using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using AemulusModManager.Avalonia.Utilities;

namespace AemulusModManager.Avalonia;

public static class Info
{
    public const string Name = "Aemulus Mod Manager";
    public const string Author = "Alexankitty";
}
class Program
{
    static Mutex? mutex;

    [STAThread]
    public static void Main(string[] args)
    {
        // get application GUID as defined in AssemblyInfo.cs
        var appGuid = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<GuidAttribute>()?.Value;

        string mutexId = string.Format("Global\\{{{0}}}", appGuid);

        bool createdNew;
        mutex = new Mutex(true, mutexId, out createdNew);

        if (!createdNew)
        {
            if(args.Length >= 1){
                if(args[0].Contains("://")){
                    var ipc = new IPCService();
                    ipc.SendMessage(args[0]);
                    return;
                }
            }
            BuildAvaloniaApp().Start(Error, [$"Another instance of {Info.Name} is already running."]);
        }
        else
        {
            BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        }
        mutex.ReleaseMutex();
    }


    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
    static void Error(Application app, string[] args)
    {
        var cts = new CancellationTokenSource();
        var errorDialog = new Views.ErrorDialog(args[0], cts);
        errorDialog.Show();
        app.Run(cts.Token);
    }
}
