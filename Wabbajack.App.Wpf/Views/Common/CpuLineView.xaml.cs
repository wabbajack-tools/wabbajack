using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using System.Windows;
using Wabbajack.Paths;
using System.IO;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CpuLineView.xaml
/// </summary>
public partial class CpuLineView : ReactiveUserControl<CPUDisplayVM>
{
    private const string _ExtractingText = "Extracting";
    private const string _DownloadingText = "Downloading";
    private const string _HashingText = "Hashing";
    public CpuLineView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel.ProgressPercent)
                .Select(x => x.Value)
                .BindToStrict(this, x => x.BackgroundProgressBar.Value)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.Msg)
                .ObserveOnGuiThread()
                .Subscribe(msg =>
                {
                    if (msg.StartsWith(_ExtractingText))
                    {
                        msg = msg.Substring(_ExtractingText.Length);

                        // Assuming it's a path here, show the file name instead of the entire path
                        try
                        {
                            msg = Path.GetFileName(msg);
                        }
                        catch(Exception ) { }

                        Icon.Visibility = Visibility.Visible;
                        Icon.Symbol = FluentIcons.Common.Symbol.Dock;
                    }
                    else if (msg.StartsWith(_DownloadingText))
                    {
                        msg = msg.Substring(_DownloadingText.Length);
                        Icon.Visibility = Visibility.Visible;
                        Icon.Symbol = FluentIcons.Common.Symbol.ArrowDownload;
                    }
                    else if (msg.StartsWith(_HashingText))
                    {
                        msg = msg.Substring(_HashingText.Length);
                        Icon.Visibility = Visibility.Visible;
                        Icon.Symbol = FluentIcons.Common.Symbol.NumberSymbol;
                    }
                    else
                    {
                        Icon.Visibility = Visibility.Collapsed;
                    }
                    Text.Text = msg;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.ProgressPercent)
                .Select(x => (int)(x.Value * 100) + "%")
                .BindToStrict(this, x => x.Progress.Text)
                .DisposeWith(dispose);
        });
    }
}
