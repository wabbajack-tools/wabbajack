using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;
using ReactiveUI;
using Wabbajack.Installer.Preflight;
using Symbol = FluentIcons.Common.Symbol;

namespace Wabbajack;

/// <summary>
/// Interaction logic for ManualDownloadView.xaml
/// </summary>
public partial class ManualDownloadView : ReactiveUserControl<ManualDownloadsVM>
{
    public ManualDownloadView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel.HeaderText)
                .BindToStrict(this, x => x.HeaderText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.CurrentSizeText)
                .BindToStrict(this, x => x.SizeText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.Current)
                .Select(current => current?.Name ?? string.Empty)
                .BindToStrict(this, x => x.FileNameText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.SiteName)
                .BindToStrict(this, x => x.SiteNameText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.Instructions)
                .BindToStrict(this, x => x.InstructionsText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.HasCurrent)
                .Select(has => has ? Visibility.Visible : Visibility.Collapsed)
                .BindToStrict(this, x => x.CardBody.Visibility)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.WatchStatusText)
                .BindToStrict(this, x => x.WatchStatusText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.WatchState)
                .ObserveOnGuiThread()
                .Subscribe(SetWatchState)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.IsWrongFile)
                .BindToStrict(this, x => x.Card.Failure)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.NoticeText)
                .ObserveOnGuiThread()
                .Subscribe(notice =>
                {
                    NoticeText.Text = notice;
                    NoticeText.Visibility = string.IsNullOrWhiteSpace(notice) ? Visibility.Collapsed : Visibility.Visible;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.WatchFolderText)
                .ObserveOnGuiThread()
                .Subscribe(folder =>
                {
                    WatchFolderText.Text = folder;
                    WatchFolderText.ToolTip = string.IsNullOrWhiteSpace(folder) ? null : folder;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.TotalCount, x => x.ViewModel.ShowAll)
                .ObserveOnGuiThread()
                .Subscribe(t =>
                {
                    var (total, showAll) = t;
                    ShowAllButton.Text = showAll ? "Hide list" : $"Show all {total}";
                    ShowAllButton.Icon = showAll ? Symbol.ChevronUp : Symbol.ChevronDown;
                    ListBorder.Visibility = showAll ? Visibility.Visible : Visibility.Collapsed;
                    ListRow.Height = showAll ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
                })
                .DisposeWith(dispose);

            this.OneWayBind(ViewModel, vm => vm.Rows, v => v.RowsList.ItemsSource)
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.OpenPageCommand, v => v.OpenPageButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.SkipCommand, v => v.SkipButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.PickFileCommand, v => v.PickFileButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.RetryCommand, v => v.RetryButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.ToggleShowAllCommand, v => v.ShowAllButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.WatchFolderPicker.SetTargetPathCommand, v => v.ChangeFolderButton)
                .DisposeWith(dispose);
        });
    }

    private void SetWatchState(ManualDownloadState state)
    {
        var watching = state is ManualDownloadState.Pending or ManualDownloadState.Detected
            or ManualDownloadState.Waiting or ManualDownloadState.Verifying;
        WatchRing.IsActive = watching;
        WatchRing.Visibility = watching ? Visibility.Visible : Visibility.Collapsed;
        WatchIcon.Visibility = watching ? Visibility.Collapsed : Visibility.Visible;

        var attention = state is ManualDownloadState.WrongFile or ManualDownloadState.Failed;
        WatchIcon.Symbol = attention ? Symbol.Warning : Symbol.CheckmarkCircle;
        WatchIcon.Foreground = Brush(attention ? "WarningBrush" : "SuccessBrush");
        WatchStatusText.Foreground = Brush(attention ? "WarningBrush" : "ForegroundBrush");
        RetryButton.Visibility = attention ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Brush Brush(string key)
    {
        return (Brush) Application.Current.Resources[key];
    }
}
