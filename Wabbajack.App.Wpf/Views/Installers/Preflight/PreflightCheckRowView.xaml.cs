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
/// Interaction logic for PreflightCheckRowView.xaml
/// </summary>
public partial class PreflightCheckRowView : ReactiveUserControl<PreflightCheckVM>
{
    public PreflightCheckRowView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel.Title)
                .BindToStrict(this, x => x.TitleText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.StatusText)
                .ObserveOnGuiThread()
                .Subscribe(text =>
                {
                    StatusText.Text = text;
                    StatusText.ToolTip = string.IsNullOrWhiteSpace(text) ? null : text;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.Progress, x => x.ViewModel.HasProgress)
                .ObserveOnGuiThread()
                .Subscribe(t => BackgroundProgressBar.Value = t.Item2 ? t.Item1.Value : 0)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.State)
                .ObserveOnGuiThread()
                .Subscribe(SetState)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.IsActive)
                .ObserveOnGuiThread()
                .Subscribe(active =>
                {
                    RowGrid.Background = Brush(active ? "ComplementaryPrimary12Brush" : "ComplementaryPrimary08Brush");
                    ActiveRule.Visibility = active ? Visibility.Visible : Visibility.Hidden;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.ActionLabel)
                .BindToStrict(this, x => x.ActionButton.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.HasAction)
                .Select(has => has ? Visibility.Visible : Visibility.Collapsed)
                .BindToStrict(this, x => x.ActionButton.Visibility)
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.ActionCommand, v => v.ActionButton)
                .DisposeWith(dispose);
        });
    }

    private void SetState(PreflightState state)
    {
        var running = state == PreflightState.Running;
        RunningRing.IsActive = running;
        RunningRing.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        StateIcon.Visibility = running ? Visibility.Collapsed : Visibility.Visible;

        (StateIcon.Symbol, StateIcon.Foreground) = state switch
        {
            PreflightState.Passed => (Symbol.CheckmarkCircle, Brush("SuccessBrush")),
            PreflightState.Warning => (Symbol.Warning, Brush("WarningBrush")),
            PreflightState.NeedsUser => (Symbol.Warning, Brush("WarningBrush")),
            PreflightState.Failed => (Symbol.ErrorCircle, Brush("ErrorBrush")),
            PreflightState.Skipped => (Symbol.SubtractCircle, Brush("ComplementaryWhite25Brush")),
            PreflightState.Cancelled => (Symbol.DismissCircle, Brush("ComplementaryWhite25Brush")),
            _ => (Symbol.Circle, Brush("ComplementaryWhite25Brush"))
        };

        ActionButton.Icon = state switch
        {
            PreflightState.NeedsUser => Symbol.Person,
            PreflightState.Warning => Symbol.Checkmark,
            PreflightState.Cancelled => Symbol.Play,
            _ => Symbol.ArrowCounterclockwise
        };
    }

    private static Brush Brush(string key)
    {
        return (Brush) Application.Current.Resources[key];
    }
}
