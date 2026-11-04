using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using FluentIcons.Common;
using ReactiveUI;
using Wabbajack.App.Avalonia.Converters;
using Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;

namespace Wabbajack.App.Avalonia.Views.Installers.Preflight;

public partial class GameFilesView : ReactiveUserControl<GameFilesVM>
{
    // WPF's Transparent66ForegroundBrush for the neutral status icon, as the plain blend that lands on its
    // gamma-space blend over the card.
    private static readonly IBrush NoteIcon = new SolidColorBrush(Color.Parse("#CAE5E5E8"));

    public GameFilesView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel!.Items)
                .Subscribe(items => RowsList.ItemsSource = items)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.HeaderText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => HeaderText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.SummaryText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => SummaryText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.MessageText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => MessageText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.ExplainText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t => ExplainText.Text = t)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.ConsequenceText)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(text =>
                {
                    ConsequenceText.Text = text;
                    ConsequenceRow.IsVisible = !string.IsNullOrWhiteSpace(text);
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.Tone)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(tone => Card.Failure = tone == GameFilesTone.Problem)
                .DisposeWith(dispose);

            // The status row is the only part that appears and disappears: before the user has asked for
            // anything there is nothing to report, and an empty row would just push the buttons down.
            this.WhenAnyValue(x => x.ViewModel!.StatusText, x => x.ViewModel!.Tone)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t =>
                {
                    var (status, tone) = t;
                    var working = tone == GameFilesTone.Working;

                    StatusRow.IsVisible = working || !string.IsNullOrWhiteSpace(status);
                    StatusText.Text = status;
                    StatusRing.IsActive = working;
                    StatusRing.IsVisible = working;
                    StatusIcon.IsVisible = !working;

                    // A user who was asked to log in and said no is neither a tick nor a cross.
                    (StatusIcon.Symbol, StatusIcon.Foreground) = tone switch
                    {
                        GameFilesTone.Done => (Symbol.CheckmarkCircle, PreflightConverters.Brush("SuccessBrush")),
                        GameFilesTone.Problem => (Symbol.ErrorCircle, PreflightConverters.Brush("ErrorBrush")),
                        _ => (Symbol.Info, NoteIcon)
                    };
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.ActionLabel)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(label =>
                {
                    RepairButton.Text = label;
                    RepairButton.IsVisible = !string.IsNullOrEmpty(label);
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.NeedsLogin)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(needsLogin => RepairButton.Icon = needsLogin ? Symbol.PersonArrowRight : Symbol.ArrowDownload)
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.RepairCommand, v => v.RepairButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.CheckAgainCommand, v => v.CheckAgainButton)
                .DisposeWith(dispose);
        });
    }
}
