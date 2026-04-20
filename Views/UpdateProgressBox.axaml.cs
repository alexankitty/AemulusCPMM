using System.Threading;
using Avalonia.Controls;
using AemulusModManager.Avalonia.ViewModels;

namespace AemulusModManager.Avalonia.Views;

public partial class UpdateProgressBox : Window
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    public bool Finished { get; set; }

    public UpdateProgressBox()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        InitializeComponent();
    }

    public UpdateProgressBox(DialogWindowViewModel dialogVm, CancellationTokenSource cancellationTokenSource) : this()
    {
        DataContext = dialogVm;
        foreach(var prop in dialogVm.AccentProps)
        {
            this.Resources[prop] = dialogVm.GetType().GetProperty(prop)?.GetValue(dialogVm);
        }
        _cancellationTokenSource = cancellationTokenSource;
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!Finished)
            _cancellationTokenSource.Cancel();
        base.OnClosing(e);
    }
}
