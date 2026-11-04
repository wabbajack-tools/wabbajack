using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.Controls;
using Wabbajack.App.Avalonia.LoginManagers;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Views;

/// <summary>The Steam login pane. Behaviour as in the WPF app's SteamLoginView.</summary>
public partial class SteamLoginView : ReactiveUserControl<SteamLoginVM>
{
    public SteamLoginView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel!.Mode)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(mode =>
                {
                    var qr = mode == SteamLoginMode.QrCode;
                    QrPane.IsVisible = qr;
                    CredentialsPane.IsVisible = !qr;
                    SignInButton.IsVisible = !qr;
                    if (!qr) UsernameBox.Focus();
                })
                .DisposeWith(dispose);

            // Steam rotates the challenge every few seconds, so this is a redraw rather than a one-off.
            this.WhenAnyValue(x => x.ViewModel!.ChallengeUrl)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(url =>
                {
                    Qr.Text = url;
                    var waiting = string.IsNullOrEmpty(url);
                    QrRing.IsActive = waiting;
                    QrRing.IsVisible = waiting;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.StatusText, x => x.ViewModel!.ErrorText, x => x.ViewModel!.IsBusy,
                    x => x.ViewModel!.Mode)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(t =>
                {
                    var (status, error, busy, mode) = t;
                    var failed = !string.IsNullOrWhiteSpace(error);

                    StatusText.Text = failed ? error : status;
                    StatusText.Foreground = Brush(failed ? "ErrorBrush" : "ForegroundBrush");
                    ErrorIcon.IsVisible = failed;
                    BusyRing.IsActive = busy && !failed;
                    BusyRing.IsVisible = busy && !failed;

                    // In the credentials pane Sign in is already the button that tries again; only the QR
                    // pane, which has nothing else to press, needs one of its own.
                    RetryButton.IsVisible = failed && mode == SteamLoginMode.QrCode;
                })
                .DisposeWith(dispose);

            // The password never reaches the view model; only whether there is one, so the button can say
            // when it is ready. It is read out of the box and handed to the command at the moment of use.
            this.WhenAnyValue(x => x.PasswordBox.Text)
                .Subscribe(p => ViewModel!.HasPassword = !string.IsNullOrEmpty(p))
                .DisposeWith(dispose);

            ViewModel!.SignInCommand.CanExecute
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(can => SignInButton.IsEnabled = can)
                .DisposeWith(dispose);

            SignInButton.Click += OnSignIn;
            PasswordBox.KeyDown += OnPasswordKeyDown;
            GuardCodeBox.KeyDown += OnGuardKeyDown;
            GuardSubmitButton.Click += OnGuardSubmit;
            Disposable.Create(() =>
            {
                SignInButton.Click -= OnSignIn;
                PasswordBox.KeyDown -= OnPasswordKeyDown;
                GuardCodeBox.KeyDown -= OnGuardKeyDown;
                GuardSubmitButton.Click -= OnGuardSubmit;
            }).DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.Guard.Pending)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ApplyGuard)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.GuardCodeBox.Text)
                .Subscribe(text =>
                {
                    if (ViewModel?.Guard.Pending is { } pending) pending.Code = text ?? string.Empty;
                    GuardSubmitButton.IsEnabled = !string.IsNullOrWhiteSpace(text);
                })
                .DisposeWith(dispose);
        });
    }

    private void OnSignIn(object? sender, RoutedEventArgs e) => SignIn();

    private void OnPasswordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SignInButton.IsEnabled) SignIn();
    }

    private void OnGuardKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Submit();
    }

    private void OnGuardSubmit(object? sender, RoutedEventArgs e) => Submit();

    private void SignIn() => ViewModel!.SignInCommand.Execute(PasswordBox.Text ?? string.Empty).Subscribe();

    /// <summary>
    ///     Draws whatever Steam Guard is asking. A device confirmation is answered on the user's phone, so it
    ///     is shown with nothing to type and no button to press.
    /// </summary>
    private void ApplyGuard(SteamGuardRequest? pending)
    {
        GuardCard.IsVisible = pending != null;

        if (pending == null)
        {
            SignInButton.ButtonStyle = ButtonStyle.Color;
            return;
        }

        GuardTitle.Text = pending.Title;
        GuardMessage.Text = pending.Message;
        GuardRetryText.IsVisible = pending.PreviousCodeWasIncorrect;

        // One primary action on screen at a time: while Steam Guard is asking, answering it is the thing to do.
        SignInButton.ButtonStyle = ButtonStyle.Mono;

        GuardCodeBox.IsVisible = pending.NeedsCode;
        GuardSubmitButton.IsVisible = pending.NeedsCode;

        if (!pending.NeedsCode) return;

        GuardCodeBox.Text = string.Empty;
        GuardSubmitButton.IsEnabled = false;
        GuardCodeBox.Focus();
    }

    /// <summary>
    ///     Answers the question on screen. Guarded on the text rather than the command, because executing a
    ///     ReactiveCommand that refuses pushes the refusal into ThrownExceptions, which nothing observes.
    /// </summary>
    private void Submit()
    {
        if (ViewModel?.Guard.Pending is not { } pending) return;
        if (!string.IsNullOrWhiteSpace(pending.Code)) pending.SubmitCommand.Execute().Subscribe();
    }

    private static IBrush? Brush(string key) =>
        Application.Current?.FindResource(key) as IBrush;
}
