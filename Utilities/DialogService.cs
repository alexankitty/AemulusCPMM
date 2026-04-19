using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AemulusModManager.Avalonia.ViewModels;
using AemulusModManager.Avalonia.Views;
using AemulusModManager.Utilities.PackageUpdating;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Octokit;

namespace AemulusModManager.Avalonia.Utilities;

public class DialogService
{
    private readonly Window _owner;
    public Window OwnerWindow => _owner;

    public DialogService(Window owner)
    {
        _owner = owner;
    }

    public async Task<bool> ShowNotification(string message, bool isOkOnly = true)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = new NotificationBox(message, isOkOnly);
            await box.ShowDialog(_owner);
            return box.YesNo;
        });
    }

    public async Task<(bool yesNo, string? skippedVersion)> ShowChangelog(
        GameBananaItemUpdate update, string packageName, string message,
        DisplayedMetadata row, string onlineVersion, string packageXmlPath, bool isUpdate)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = new ChangelogBox(update, packageName, message, row, onlineVersion, packageXmlPath, isUpdate);
            await box.ShowDialog(_owner);
            return (box.YesNo, null as string);
        });
    }

    public async Task<bool> ShowDownloadConfirm(string name, string? author = null, Uri? imageUri = null)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = new DownloadWindow(name, author ?? "", imageUri);
            await box.ShowDialog(_owner);
            return box.YesNo;
        });
    }

    public async Task<bool> ShowDownloadConfirm(GameBananaRecord record)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = new DownloadWindow(record);
            await box.ShowDialog(_owner);
            return box.YesNo;
        });
    }

    public async Task<(string? url, string? fileName)> ShowFileSelector(
        List<GameBananaItemFile> files, string packageName)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = new UpdateFileBox(files, packageName);
            await box.ShowDialog(_owner);
            return (box.ChosenFileUrl, box.ChosenFileName);
        });
    }

    public async Task<(string? url, string? fileName)> ShowFileSelector(
        IReadOnlyList<ReleaseAsset> files, string packageName)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = new UpdateFileBox(files, packageName);
            await box.ShowDialog(_owner);
            return (box.ChosenFileUrl, box.ChosenFileName);
        });
    }

    public async Task ShowAltLinks(List<GameBananaAlternateFileSource>? sources, string name, string game, bool isUpdate)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = new AltLinkWindow(sources ?? new List<GameBananaAlternateFileSource>());
            await box.ShowDialog(_owner);
        });
    }

    public UpdateProgressBox CreateProgressBox(System.Threading.CancellationTokenSource cts)
    {
        return new UpdateProgressBox(cts);
    }

    public async Task ShowProgressBox(UpdateProgressBox box)
    {
        await Dispatcher.UIThread.InvokeAsync(() => box.Show(_owner));
    }

    public async Task CloseProgressBox(UpdateProgressBox box)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            box.Finished = true;
            box.Close();
        });
    }

    public async Task UpdateProgress(UpdateProgressBox box, double percentage, string title, string text)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var progressBar = box.FindControl<ProgressBar>("ProgressBar");
            var progressTitle = box.FindControl<TextBlock>("ProgressTitle");
            var progressText = box.FindControl<TextBlock>("ProgressText");
            if (progressBar != null) progressBar.Value = percentage * 100;
            if (progressTitle != null) progressTitle.Text = title;
            if (progressText != null) progressText.Text = text;
            if (percentage >= 1) box.Finished = true;
        });
    }

    public async Task<(string? name, bool copyLoadout)> ShowInputDialog(DialogWindowViewModel viewModel, string prompt)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = new InputBox(viewModel, prompt);
            await box.ShowDialog(_owner);
            return (box.Result, box.CopyLoadout);
        });
    }

    public async Task<bool> ShowConfirmation(string message)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var box = new NotificationBox(message, false);
            await box.ShowDialog(_owner);
            return box.YesNo;
        });
    }

    public async Task<string?> ShowSaveFileDialog(string defaultFileName, string filterName, string filterExtension)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var storageProvider = _owner.StorageProvider;
            var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save File",
                SuggestedFileName = defaultFileName,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(filterName) { Patterns = new[] { filterExtension } }
                }
            });
            return result?.Path.LocalPath;
        });
    }
}
