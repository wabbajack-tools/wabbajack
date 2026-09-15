using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Input;
using ReactiveMarbles.ObservableEvents;
using ReactiveUI;
using Wabbajack.LoginManagers;

namespace Wabbajack;

/// <summary>
/// Interaction logic for SteamLoginView.xaml
/// </summary>
public partial class SteamLoginView : ReactiveUserControl<SteamLoginVM>
{
    public SteamLoginView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel.Mode)
                .ObserveOnGuiThread()
                .Subscribe(mode =>
                {
                    var qr = mode == SteamLoginMode.QrCode;
                    QrPane.Visibility = qr ? Visibility.Visible : Visibility.Collapsed;
                    CredentialsPane.Visibility = qr ? Visibility.Collapsed : Visibility.Visible;
                    SignInButton.Visibility = qr ? Visibility.Collapsed : Visibility.Visible;
                    if (!qr) UsernameBox.Focus();
                })
                .DisposeWith(dispose);

            // Steam rotates the challenge every few seconds, so this is a redraw rather than a one-off.
            this.WhenAnyValue(x => x.ViewModel.ChallengeUrl)
                .ObserveOnGuiThread()
                .Subscribe(url =>
                {
                    Qr.Text = url;
                    var waiting = string.IsNullOrEmpty(url);
                    QrRing.IsActive = waiting;
                    QrRing.Visibility = waiting ? Visibility.Visible : Visibility.Collapsed;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.StatusText, x => x.ViewModel.ErrorText, x => x.ViewModel.IsBusy,
                    x => x.ViewModel.Mode)
                .ObserveOnGuiThread()
                .Subscribe(t =>
                {
                    var (status, error, busy, mode) = t;
                    var failed = !string.IsNullOrWhiteSpace(error);

                    StatusText.Text = failed ? error : status;
                    StatusText.Foreground = Brush(failed ? "ErrorBrush" : "ForegroundBrush");
                    ErrorIcon.Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
                    BusyRing.IsActive = busy && !failed;
                    BusyRing.Visibility = busy && !failed ? Visibility.Visible : Visibility.Collapsed;

                    // In the credentials pane Sign in is already the button that tries again; only the QR
                    // pane, which has nothing else to press, needs one of its own.
                    RetryButton.Visibility = failed && mode == SteamLoginMode.QrCode
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                })
                .DisposeWith(dispose);

            this.Bind(ViewModel, vm => vm.Username, v => v.UsernameBox.Text)
                .DisposeWith(dispose);

            // The password never reaches the view model; only whether there is one, so the button can say
            // when it is ready. It is read out of the box and handed to the command at the moment of use.
            var password = PasswordBox.Events().PasswordChanged
                .Select(_ => PasswordBox.Password)
                .StartWith(string.Empty);

            password
                .Subscribe(p => ViewModel!.HasPassword = !string.IsNullOrEmpty(p))
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.SignInCommand, v => v.SignInButton, password)
                .DisposeWith(dispose);

            PasswordBox.Events().KeyDown
                .Where(e => e.Key == Key.Enter)
                .Subscribe(_ =>
                {
                    if (SignInButton.IsEnabled) ViewModel!.SignInCommand.Execute(PasswordBox.Password).Subscribe();
                })
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.UseCredentialsCommand, v => v.UseCredentialsButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.UseQrCommand, v => v.UseQrButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.UseQrCommand, v => v.RetryButton)
                .DisposeWith(dispose);
            this.BindCommand(ViewModel, vm => vm.CloseCommand, v => v.CancelButton)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.Guard.Pending)
                .ObserveOnGuiThread()
                .Subscribe(ApplyGuard)
                .DisposeWith(dispose);

            GuardCodeBox.Events().TextChanged
                .Subscribe(_ =>
                {
                    if (ViewModel?.Guard.Pending is {} pending) pending.Code = GuardCodeBox.Text;
                    GuardSubmitButton.IsEnabled = !string.IsNullOrWhiteSpace(GuardCodeBox.Text);
                })
                .DisposeWith(dispose);

            GuardCodeBox.Events().KeyDown
                .Where(e => e.Key == Key.Enter)
                .Subscribe(_ => Submit())
                .DisposeWith(dispose);

            GuardSubmitButton.Events().Click
                .Subscribe(_ => Submit())
                .DisposeWith(dispose);
        });
    }

    /// <summary>
    ///     Draws whatever Steam Guard is asking. A device confirmation is answered on the user's phone, so it
    ///     is shown with nothing to type and no button to press.
    /// </summary>
    private void ApplyGuard(SteamGuardRequest? pending)
    {
        GuardCard.Visibility = pending == null ? Visibility.Collapsed : Visibility.Visible;

        if (pending == null)
        {
            SignInButton.ButtonStyle = ButtonStyle.Color;
            return;
        }

        GuardTitle.Text = pending.Title;
        GuardMessage.Text = pending.Message;
        GuardRetryText.Visibility = pending.PreviousCodeWasIncorrect ? Visibility.Visible : Visibility.Collapsed;

        // One primary action on screen at a time. While Steam Guard is asking, answering it is the only
        // thing to do - Sign in is disabled behind it anyway, since a login is already in flight - so the
        // colour follows the question and comes back when it is answered.
        SignInButton.ButtonStyle = ButtonStyle.Mono;

        var typed = pending.NeedsCode ? Visibility.Visible : Visibility.Collapsed;
        GuardCodeBox.Visibility = typed;
        GuardSubmitButton.Visibility = typed;

        if (!pending.NeedsCode) return;

        GuardCodeBox.Text = string.Empty;
        GuardSubmitButton.IsEnabled = false;
        GuardCodeBox.Focus();
    }

    /// <summary>
    ///     Answers the question on screen. Guarded on the text rather than on the command, because executing
    ///     a <see cref="ReactiveCommand" /> that is refusing pushes the refusal into ThrownExceptions, and
    ///     nothing observes a request's.
    /// </summary>
    private void Submit()
    {
        if (ViewModel?.Guard.Pending is not {} pending) return;
        if (!string.IsNullOrWhiteSpace(pending.Code)) pending.SubmitCommand.Execute().Subscribe();
    }

    private static System.Windows.Media.Brush Brush(string key)
    {
        return (System.Windows.Media.Brush) Application.Current.Resources[key];
    }
}
