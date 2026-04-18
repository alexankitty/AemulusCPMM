using System.Threading;
using Avalonia.Controls;

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

    public UpdateProgressBox(CancellationTokenSource cancellationTokenSource)
    {
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
