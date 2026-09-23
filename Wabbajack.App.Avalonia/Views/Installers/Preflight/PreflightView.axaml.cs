using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using FluentIcons.Common;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;
using Wabbajack.Installer.Preflight;

namespace Wabbajack.App.Avalonia.Views.Installers.Preflight;

public partial class PreflightView : ReactiveUserControl<PreflightVM>
{
    /// <summary>
    ///     What the detail panel is kept clear of, measured from a manual download card: its own header row,
    ///     the margins around it, and a card with every line the user acts on. The checklist gets what is
    ///     left over, which at the default window is about five of its rows.
    /// </summary>
    private const double DetailPanelMinHeight = 290;

    /// <summary>
    ///     One row. At the window's minimum height something has to give, and the card is what the user acts
    ///     on; the checklist keeps scrolling, and the running check is still the row it scrolls to.
    /// </summary>
    private const double ChecklistMinHeight = 48;

    /// <summary>What the checklist gives up on top of that when the manual queue is opened under the card.</summary>
    private const double OpenQueueExtraHeight = 140;

    private bool _queueOpen;

    public PreflightView()
    {
        InitializeComponent();

        // WPF sized the image 16:9 from its own width through MathConverter, and rounded layout to the pixel.
        DetailImage.GetObservable(BoundsProperty)
            .Subscribe(b => DetailImage.Height = Math.Round(b.Width / (16.0 / 9.0)));

        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel!.Checks)
                .Subscribe(checks => ChecksList.ItemsSource = checks)
                .DisposeWith(dispose);

            RightColumn.GetObservable(BoundsProperty)
                .Subscribe(_ => CapChecklistHeight())
                .DisposeWith(dispose);

            // A check that has passed is of no use to someone watching progress, so the list follows the
            // one that is running.
            this.WhenAnyValue(x => x.ViewModel!.ActiveCheck)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => ScrollActiveIntoView())
                .DisposeWith(dispose);

            // Set by hand, as in WPF, so the sub-views let go of the page after Back and their view models
            // are deactivated.
            this.WhenAnyValue(x => x.ViewModel)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(vm =>
                {
                    BulkView.ViewModel = vm?.BulkDownloads;
                    ManualView.ViewModel = vm?.ManualDownloads;
                    GameFilesView.ViewModel = vm?.GameFiles;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.SummaryText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => SummaryText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.OverallPercent)
                .Select(p => p.Value)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(v => SummaryProgress.Value = v)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.DetailKind)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(kind =>
                {
                    TextDetail.IsVisible = kind == PreflightDetailKind.None;
                    BulkView.IsVisible = kind == PreflightDetailKind.BulkDownloads;
                    ManualView.IsVisible = kind == PreflightDetailKind.ManualDownloads;
                    GameFilesView.IsVisible = kind == PreflightDetailKind.GameFiles;
                    ShowAllButton.IsVisible = kind == PreflightDetailKind.ManualDownloads;
                })
                .DisposeWith(dispose);

            // The queue disclosure sits in the panel header rather than under the card, where it cost the
            // card a row of its own.
            this.WhenAnyValue(x => x.ViewModel!.ManualDownloads.TotalCount, x => x.ViewModel!.ManualDownloads.ShowAll)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t =>
                {
                    var (total, showAll) = t;
                    ShowAllButton.Text = showAll ? "Hide list" : $"Show all {total}";
                    ShowAllButton.Icon = showAll ? Symbol.ChevronUp : Symbol.ChevronDown;
                    _queueOpen = showAll;
                    CapChecklistHeight();
                })
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.ManualDownloads.ToggleShowAllCommand, v => v.ShowAllButton)
                .DisposeWith(dispose);

            // The header and text body follow whichever check is active, including its later updates.
            this.WhenAnyValue(x => x.ViewModel!.ActiveCheck)
                .Select(check => check == null
                    ? this.WhenAnyValue(x => x.ViewModel!.AllPassed, x => x.ViewModel!.IsRunning)
                        .Select(t => (Title: t.Item1 ? "All checks passed" : t.Item2 ? "Starting" : "Checks stopped",
                            Message: t.Item1 ? "Everything the install needs is in place. Install when you are ready." : string.Empty,
                            Detail: (string?)null,
                            State: t.Item1 ? PreflightState.Passed : PreflightState.Pending))
                    : check.WhenAnyValue(c => c.Title, c => c.Message, c => c.DetailText, c => c.State)
                        .Select(t => (Title: t.Item1, Message: t.Item2, Detail: t.Item3, State: t.Item4)))
                .Switch()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t =>
                {
                    DetailTitle.Text = t.Title;
                    DetailMessage.Text = t.Message;
                    DetailText.Text = t.Detail ?? string.Empty;
                    DetailText.IsVisible = !string.IsNullOrWhiteSpace(t.Detail);
                    DetailIcon.Symbol = t.State switch
                    {
                        PreflightState.Passed => Symbol.CheckmarkCircle,
                        PreflightState.Failed => Symbol.ErrorCircle,
                        PreflightState.NeedsUser or PreflightState.Warning => Symbol.Warning,
                        PreflightState.Running => Symbol.ArrowSync,
                        _ => Symbol.TaskListSquare
                    };
                })
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.BackCommand, v => v.BackButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.InstallCommand, v => v.InstallButton)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.ViewModel!.OpenReadmeCommand)
                .Subscribe(cmd => DocumentationButton.Command = cmd)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.ViewModel!.OpenWebsiteCommand)
                .Subscribe(cmd => WebsiteButton.Command = cmd)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.ViewModel!.OpenCommunityCommand)
                .Subscribe(cmd => CommunityButton.Command = cmd)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.ViewModel!.OpenManifestCommand)
                .Subscribe(cmd => ManifestButton.Command = cmd)
                .DisposeWith(dispose);
        });
    }

    /// <summary>
    ///     Hands the checklist whatever height is left once the detail panel has its minimum. The header and
    ///     the page actions are measured rather than assumed, so only the panel's own floor is a constant.
    ///     The panel's margins come out of its row, so they are reserved on top of that floor.
    /// </summary>
    private void CapChecklistHeight()
    {
        var wanted = DetailPanelMinHeight + DetailPanel.Margin.Top + DetailPanel.Margin.Bottom
                     + (_queueOpen ? OpenQueueExtraHeight : 0);
        var spare = RightColumn.Bounds.Height - PageHeader.Bounds.Height - PageActions.Bounds.Height
                    - PageHeader.Margin.Bottom - wanted;
        ChecksScroller.MaxHeight = Math.Max(ChecklistMinHeight, spare);
        ScrollActiveIntoView();
    }

    /// <summary>
    ///     Scrolls the running check back into view. Queued, because the row's container is only in place
    ///     after the layout pass the change itself causes.
    /// </summary>
    private void ScrollActiveIntoView()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ViewModel?.ActiveCheck is not { } check) return;
            var index = ViewModel.Checks.IndexOf(check);
            if (index < 0) return;
            ChecksList.ContainerFromIndex(index)?.BringIntoView();
        }, DispatcherPriority.Background);
    }
}
