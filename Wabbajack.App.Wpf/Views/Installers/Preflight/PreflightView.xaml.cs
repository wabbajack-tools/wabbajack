using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Threading;
using ReactiveUI;
using Wabbajack.Installer.Preflight;
using Symbol = FluentIcons.Common.Symbol;

namespace Wabbajack;

/// <summary>
/// Interaction logic for PreflightView.xaml
/// </summary>
public partial class PreflightView : ReactiveUserControl<PreflightVM>
{
    /// <summary>
    ///     What the detail panel is kept clear of, measured from a manual download card: its own header row,
    ///     the margins around it, and a card with every line the user acts on. The checklist gets what is
    ///     left over, which at the default 1441x695 window is about five of its 44px rows.
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
        this.WhenActivated(dispose =>
        {
            this.OneWayBind(ViewModel, vm => vm.Checks, v => v.ChecksList.ItemsSource)
                .DisposeWith(dispose);

            Observable
                .FromEventPattern<SizeChangedEventHandler, SizeChangedEventArgs>(
                    h => RightColumn.SizeChanged += h, h => RightColumn.SizeChanged -= h)
                .Select(_ => Unit.Default)
                .StartWith(Unit.Default)
                .Subscribe(_ => CapChecklistHeight())
                .DisposeWith(dispose);

            // A check that has passed is of no use to someone watching progress, so the list follows the
            // one that is running.
            this.WhenAnyValue(x => x.ViewModel.ActiveCheck)
                .ObserveOnGuiThread()
                .Subscribe(_ => ScrollActiveIntoView())
                .DisposeWith(dispose);

            // Set by hand rather than bound: OneWayBind drops a null view model, which would leave the two
            // sub-views holding the page after Back and their view models never deactivated.
            this.WhenAnyValue(x => x.ViewModel)
                .ObserveOnGuiThread()
                .Subscribe(vm =>
                {
                    BulkView.ViewModel = vm?.BulkDownloads;
                    ManualView.ViewModel = vm?.ManualDownloads;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.SummaryText)
                .BindToStrict(this, x => x.SummaryText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.OverallPercent)
                .Select(p => p.Value)
                .BindToStrict(this, x => x.SummaryProgress.Value)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.DetailKind)
                .ObserveOnGuiThread()
                .Subscribe(kind =>
                {
                    TextDetail.Visibility = kind == PreflightDetailKind.None ? Visibility.Visible : Visibility.Collapsed;
                    BulkView.Visibility = kind == PreflightDetailKind.BulkDownloads ? Visibility.Visible : Visibility.Collapsed;
                    ManualView.Visibility = kind == PreflightDetailKind.ManualDownloads ? Visibility.Visible : Visibility.Collapsed;
                    ShowAllButton.Visibility = kind == PreflightDetailKind.ManualDownloads ? Visibility.Visible : Visibility.Collapsed;
                })
                .DisposeWith(dispose);

            // The queue disclosure sits in the panel header rather than under the card, where it cost the
            // card a row of its own.
            this.WhenAnyValue(x => x.ViewModel.ManualDownloads.TotalCount, x => x.ViewModel.ManualDownloads.ShowAll)
                .ObserveOnGuiThread()
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
            this.WhenAnyValue(x => x.ViewModel.ActiveCheck)
                .Select(check => check == null
                    ? this.WhenAnyValue(x => x.ViewModel.AllPassed, x => x.ViewModel.IsRunning)
                        .Select(t => (Title: t.Item1 ? "All checks passed" : t.Item2 ? "Starting" : "Checks stopped",
                            Message: t.Item1 ? "Everything the install needs is in place. Install when you are ready." : string.Empty,
                            Detail: (string?) null,
                            State: t.Item1 ? PreflightState.Passed : PreflightState.Pending))
                    : check.WhenAnyValue(c => c.Title, c => c.Message, c => c.DetailText, c => c.State)
                        .Select(t => (Title: t.Item1, Message: t.Item2, Detail: t.Item3, State: t.Item4)))
                .Switch()
                .ObserveOnGuiThread()
                .Subscribe(t =>
                {
                    DetailTitle.Text = t.Title;
                    DetailMessage.Text = t.Message;
                    DetailText.Text = t.Detail ?? string.Empty;
                    DetailText.Visibility = string.IsNullOrWhiteSpace(t.Detail) ? Visibility.Collapsed : Visibility.Visible;
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
            this.BindCommand(ViewModel, vm => vm.OpenReadmeCommand, v => v.DocumentationButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.OpenWebsiteCommand, v => v.WebsiteButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.OpenCommunityCommand, v => v.CommunityButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.OpenManifestCommand, v => v.ManifestButton)
                .DisposeWith(dispose);
        });
    }

    /// <summary>
    ///     Hands the checklist whatever height is left once the detail panel has its minimum. The header and
    ///     the page actions are measured rather than assumed, so only the panel's own floor is a constant.
    ///     The panel's margins come out of its row, so they are reserved on top of that floor; without them
    ///     the panel ends up short of what the constant promises.
    /// </summary>
    private void CapChecklistHeight()
    {
        var wanted = DetailPanelMinHeight + DetailPanel.Margin.Top + DetailPanel.Margin.Bottom
                     + (_queueOpen ? OpenQueueExtraHeight : 0);
        var spare = RightColumn.ActualHeight - PageHeader.ActualHeight - PageActions.ActualHeight
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
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ViewModel?.ActiveCheck is not { } check) return;
            var index = ViewModel.Checks.IndexOf(check);
            if (index < 0) return;
            if (ChecksList.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container)
                container.BringIntoView();
        }), DispatcherPriority.Background);
    }
}
