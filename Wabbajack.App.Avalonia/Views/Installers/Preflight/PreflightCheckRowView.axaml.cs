using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using FluentIcons.Common;
using ReactiveUI;
using Wabbajack.App.Avalonia.Converters;
using Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;
using Wabbajack.Installer.Preflight;

namespace Wabbajack.App.Avalonia.Views.Installers.Preflight;

public partial class PreflightCheckRowView : ReactiveUserControl<PreflightCheckVM>
{
    // Transparent66ForegroundBrush as WPF draws it: translucent text is blended in gamma space, which lands
    // brighter than a plain blend. These are the plain blends that land on WPF's colour over the row's two fills.
    private static readonly IBrush StatusOnRow = new SolidColorBrush(Color.Parse("#C7E5E5E8"));
    private static readonly IBrush StatusOnActiveRow = new SolidColorBrush(Color.Parse("#C6E5E5E8"));

    public PreflightCheckRowView()
    {
        InitializeComponent();
        StatusText.Foreground = StatusOnRow;

        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel!.Title)
                .Subscribe(t => TitleText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.StatusText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(text =>
                {
                    StatusText.Text = text;
                    ToolTip.SetTip(StatusText, string.IsNullOrWhiteSpace(text) ? null : text);
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.Progress, x => x.ViewModel!.HasProgress)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => BackgroundProgressBar.Value = t.Item2 ? t.Item1.Value : 0)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.State)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(SetState)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.IsActive)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(active =>
                {
                    RowGrid.Background = PreflightConverters.Brush(active ? "ComplementaryPrimary12Brush" : "ComplementaryPrimary08Brush");
                    StatusText.Foreground = active ? StatusOnActiveRow : StatusOnRow;
                    ActiveRule.Opacity = active ? 1 : 0;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.ActionLabel)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(label => ActionButton.Text = label)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.HasAction)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(has => ActionButton.IsVisible = has)
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.ActionCommand, v => v.ActionButton)
                .DisposeWith(dispose);
        });
    }

    private void SetState(PreflightState state)
    {
        var running = state == PreflightState.Running;
        RunningRing.IsActive = running;
        RunningRing.IsVisible = running;
        StateIcon.IsVisible = !running;

        (StateIcon.Symbol, StateIcon.Foreground) = state switch
        {
            PreflightState.Passed => (Symbol.CheckmarkCircle, PreflightConverters.Brush("SuccessBrush")),
            PreflightState.Warning => (Symbol.Warning, PreflightConverters.Brush("WarningBrush")),
            PreflightState.NeedsUser => (Symbol.Warning, PreflightConverters.Brush("WarningBrush")),
            PreflightState.Failed => (Symbol.ErrorCircle, PreflightConverters.Brush("ErrorBrush")),
            PreflightState.Skipped => (Symbol.SubtractCircle, PreflightConverters.Brush("ComplementaryWhite25Brush")),
            PreflightState.Cancelled => (Symbol.DismissCircle, PreflightConverters.Brush("ComplementaryWhite25Brush")),
            _ => (Symbol.Circle, PreflightConverters.Brush("ComplementaryWhite25Brush"))
        };

        ActionButton.Icon = state switch
        {
            PreflightState.NeedsUser => Symbol.Person,
            PreflightState.Warning => Symbol.Checkmark,
            PreflightState.Cancelled => Symbol.Play,
            _ => Symbol.ArrowCounterclockwise
        };
    }
}
