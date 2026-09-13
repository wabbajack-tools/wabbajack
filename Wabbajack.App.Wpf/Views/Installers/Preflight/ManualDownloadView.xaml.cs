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
    /// <summary>
    ///     One 32px row plus the border's own margin: the least the queue can show and still be a list.
    ///     Deliberately the minimum, so the card gives nothing up on any window where the queue already
    ///     had room.
    /// </summary>
    private const double QueueListMinHeight = 40;

    private bool _queueOpen;

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

            // The disclosure itself lives in the detail panel's header; this only opens and closes the list.
            // The card keeps its natural height once the list is out, so the list gets the rest of the panel.
            this.WhenAnyValue(x => x.ViewModel.ShowAll)
                .ObserveOnGuiThread()
                .Subscribe(showAll =>
                {
                    _queueOpen = showAll;
                    ListBorder.Visibility = showAll ? Visibility.Visible : Visibility.Collapsed;
                    ListRow.Height = showAll ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
                    CardRow.Height = showAll ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
                    CapCardHeight();
                })
                .DisposeWith(dispose);

            Observable
                .FromEventPattern<SizeChangedEventHandler, SizeChangedEventArgs>(
                    h => RootGrid.SizeChanged += h, h => RootGrid.SizeChanged -= h)
                .Subscribe(_ => CapCardHeight())
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
            this.BindCommand(ViewModel, vm => vm.WatchFolderPicker.SetTargetPathCommand, v => v.ChangeFolderButton)
                .DisposeWith(dispose);
        });
    }

    /// <summary>
    ///     Keeps the queue a row while it is open. The card row is Auto, so on a window short enough for
    ///     the card alone to fill the panel the star row below it would otherwise be squeezed to nothing and
    ///     "Show all" would open onto an empty strip. Capping the card instead of putting a floor under the
    ///     list is what keeps the two rows inside the panel: a floor alone pushes the list out of the bottom
    ///     of it. The card has its own ScrollViewer for exactly this.
    /// </summary>
    private void CapCardHeight()
    {
        var height = RootGrid.ActualHeight;
        var cap = _queueOpen && height > QueueListMinHeight
            ? height - QueueListMinHeight
            : double.PositiveInfinity;
        if (Math.Abs(CardRow.MaxHeight - cap) > 0.5 || double.IsInfinity(cap) != double.IsInfinity(CardRow.MaxHeight))
            CardRow.MaxHeight = cap;
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
