using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using FluentIcons.Common;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Common;

namespace Wabbajack.App.Avalonia.Views.Common;

public partial class CpuLineView : ReactiveUserControl<CPUDisplayVM>
{
    private const string ExtractingText = "Extracting";
    private const string DownloadingText = "Downloading";
    private const string HashingText = "Hashing";

    public CpuLineView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            var progress = this.WhenAnyValue(x => x.ViewModel!.ProgressPercent)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Publish()
                .RefCount();

            progress
                .Subscribe(p =>
                {
                    BackgroundProgressBar.Value = p.Value;
                    Progress.Text = (int)(p.Value * 100) + "%";
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.Msg)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ShowMessage)
                .DisposeWith(dispose);
        });
    }

    /// <summary>
    /// A job's description starts with what it is doing; that becomes the icon and the rest is shown. An
    /// extraction names a path, of which only the file name is kept.
    /// </summary>
    private void ShowMessage(string? msg)
    {
        msg ??= string.Empty;
        if (msg.StartsWith(ExtractingText))
        {
            msg = msg.Substring(ExtractingText.Length);
            try
            {
                msg = Path.GetFileName(msg);
            }
            catch (Exception) { }

            Icon.IsVisible = true;
            Icon.Symbol = Symbol.Dock;
        }
        else if (msg.StartsWith(DownloadingText))
        {
            msg = msg.Substring(DownloadingText.Length);
            Icon.IsVisible = true;
            Icon.Symbol = Symbol.ArrowDownload;
        }
        else if (msg.StartsWith(HashingText))
        {
            msg = msg.Substring(HashingText.Length);
            Icon.IsVisible = true;
            Icon.Symbol = Symbol.NumberSymbol;
        }
        else
        {
            Icon.IsVisible = false;
        }
        Text.Text = msg;
    }
}
