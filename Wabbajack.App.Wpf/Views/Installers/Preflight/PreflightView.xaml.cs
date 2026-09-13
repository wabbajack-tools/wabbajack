using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;
using Wabbajack.Installer.Preflight;
using Symbol = FluentIcons.Common.Symbol;

namespace Wabbajack;

/// <summary>
/// Interaction logic for PreflightView.xaml
/// </summary>
public partial class PreflightView : ReactiveUserControl<PreflightVM>
{
    public PreflightView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.OneWayBind(ViewModel, vm => vm.Checks, v => v.ChecksList.ItemsSource)
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
                })
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
}
