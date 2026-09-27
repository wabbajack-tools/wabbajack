using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using FluentIcons.Common;
using ReactiveUI;
using Wabbajack.App.Avalonia.Converters;
using Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;
using Wabbajack.Installer.Preflight;

namespace Wabbajack.App.Avalonia.Views.Installers.Preflight;

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
            this.WhenAnyValue(x => x.ViewModel!.HeaderText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => HeaderText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.CurrentSizeText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => SizeText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.Current)
                .Select(current => current?.Name ?? string.Empty)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => FileNameText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.SiteName)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => SiteNameText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.Instructions)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => InstructionsText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.HasCurrent)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(has => CardBody.IsVisible = has)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.WatchStatusText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => WatchStatusText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.WatchState)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(SetWatchState)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.IsWrongFile)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(wrong => Card.Failure = wrong)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.NoticeText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(notice =>
                {
                    NoticeText.Text = notice;
                    NoticeText.IsVisible = !string.IsNullOrWhiteSpace(notice);
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.WatchFolderText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(folder =>
                {
                    WatchFolderText.Text = folder;
                    ToolTip.SetTip(WatchFolderText, string.IsNullOrWhiteSpace(folder) ? null : folder);
                })
                .DisposeWith(dispose);

            // The disclosure itself lives in the detail panel's header; this only opens and closes the list.
            // The card keeps its natural height once the list is out, so the list gets the rest of the panel.
            this.WhenAnyValue(x => x.ViewModel!.ShowAll)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(showAll =>
                {
                    _queueOpen = showAll;
                    ListBorder.IsVisible = showAll;
                    RootGrid.RowDefinitions[1].Height = showAll ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
                    RootGrid.RowDefinitions[0].Height = showAll ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
                    CapCardHeight();
                })
                .DisposeWith(dispose);

            RootGrid.GetObservable(BoundsProperty)
                .Subscribe(_ => CapCardHeight())
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.Rows)
                .Subscribe(rows => RowsList.ItemsSource = rows)
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.OpenPageCommand, v => v.OpenPageButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.SkipCommand, v => v.SkipButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.PickFileCommand, v => v.PickFileButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.RetryCommand, v => v.RetryButton)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.ViewModel!.WatchFolderPicker.SetTargetPathCommand)
                .Subscribe(cmd => ChangeFolderButton.Command = cmd)
                .DisposeWith(dispose);
        });
    }

    /// <summary>
    ///     Keeps the queue a row while it is open. The card row is Auto, so on a window short enough for
    ///     the card alone to fill the panel the star row below it would otherwise be squeezed to nothing and
    ///     "Show all" would open onto an empty strip. Capping the card instead of putting a floor under the
    ///     list is what keeps the two rows inside the panel. The card has its own ScrollViewer for this.
    /// </summary>
    private void CapCardHeight()
    {
        var height = RootGrid.Bounds.Height;
        var cap = _queueOpen && height > QueueListMinHeight
            ? height - QueueListMinHeight
            : double.PositiveInfinity;
        var row = RootGrid.RowDefinitions[0];
        if (Math.Abs(row.MaxHeight - cap) > 0.5 || double.IsInfinity(cap) != double.IsInfinity(row.MaxHeight))
            row.MaxHeight = cap;
    }

    private void SetWatchState(ManualDownloadState state)
    {
        var watching = state is ManualDownloadState.Pending or ManualDownloadState.Detected
            or ManualDownloadState.Waiting or ManualDownloadState.Verifying;
        WatchRing.IsActive = watching;
        WatchRing.IsVisible = watching;
        WatchIcon.IsVisible = !watching;

        var attention = state is ManualDownloadState.WrongFile or ManualDownloadState.Failed;
        WatchIcon.Symbol = attention ? Symbol.Warning : Symbol.CheckmarkCircle;
        WatchIcon.Foreground = PreflightConverters.Brush(attention ? "WarningBrush" : "SuccessBrush");
        WatchStatusText.Foreground = PreflightConverters.Brush(attention ? "WarningBrush" : "ForegroundBrush");
        RetryButton.IsVisible = attention;
    }
}
