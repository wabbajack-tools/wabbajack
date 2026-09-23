using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;
using Symbol = FluentIcons.Common.Symbol;

namespace Wabbajack;

/// <summary>
/// Interaction logic for GameFilesView.xaml
/// </summary>
public partial class GameFilesView : ReactiveUserControl<GameFilesVM>
{
    public GameFilesView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.OneWayBind(ViewModel, vm => vm.Items, v => v.RowsList.ItemsSource)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.HeaderText)
                .BindToStrict(this, x => x.HeaderText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.SummaryText)
                .BindToStrict(this, x => x.SummaryText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.MessageText)
                .BindToStrict(this, x => x.MessageText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.ExplainText)
                .BindToStrict(this, x => x.ExplainText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.ConsequenceText)
                .ObserveOnGuiThread()
                .Subscribe(text =>
                {
                    ConsequenceText.Text = text;
                    ConsequenceRow.Visibility = string.IsNullOrWhiteSpace(text)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.Tone)
                .Select(tone => tone == GameFilesTone.Problem)
                .BindToStrict(this, x => x.Card.Failure)
                .DisposeWith(dispose);

            // The status row is the only part that appears and disappears: before the user has asked for
            // anything there is nothing to report, and an empty row would just push the buttons down.
            this.WhenAnyValue(x => x.ViewModel.StatusText, x => x.ViewModel.Tone)
                .ObserveOnGuiThread()
                .Subscribe(t =>
                {
                    var (status, tone) = t;
                    var working = tone == GameFilesTone.Working;

                    StatusRow.Visibility = working || !string.IsNullOrWhiteSpace(status)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    StatusText.Text = status;
                    StatusRing.IsActive = working;
                    StatusRing.Visibility = working ? Visibility.Visible : Visibility.Collapsed;
                    StatusIcon.Visibility = working ? Visibility.Collapsed : Visibility.Visible;

                    // A user who was asked to log in and said no is neither a tick nor a cross.
                    (StatusIcon.Symbol, StatusIcon.Foreground) = tone switch
                    {
                        GameFilesTone.Done => (Symbol.CheckmarkCircle, Brush("SuccessBrush")),
                        GameFilesTone.Problem => (Symbol.ErrorCircle, Brush("ErrorBrush")),
                        _ => (Symbol.Info, Brush("Transparent66ForegroundBrush"))
                    };
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.ActionLabel)
                .BindToStrict(this, x => x.RepairButton.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.NeedsLogin)
                .ObserveOnGuiThread()
                .Subscribe(needsLogin =>
                    RepairButton.Icon = needsLogin ? Symbol.PersonArrowRight : Symbol.ArrowDownload)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.ActionLabel)
                .Select(label => string.IsNullOrEmpty(label) ? Visibility.Collapsed : Visibility.Visible)
                .BindToStrict(this, x => x.RepairButton.Visibility)
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.RepairCommand, v => v.RepairButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.CheckAgainCommand, v => v.CheckAgainButton)
                .DisposeWith(dispose);
        });
    }

    private static System.Windows.Media.Brush Brush(string key)
    {
        return (System.Windows.Media.Brush) Application.Current.Resources[key];
    }
}
