using System;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveMarbles.ObservableEvents;
using System.Windows.Input;
using System.Windows;
using System.IO;
using Wabbajack.Paths;
using System.Windows.Media;
using Wabbajack.LoginManagers;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;

namespace Wabbajack;

public partial class MegaLoginView : ReactiveUserControl<MegaLoginVM>
{
    public MegaLoginView()
    {
        InitializeComponent();

        MegaImage.Source = BitmapFrame.Create(typeof(MegaLoginManager).Assembly.GetManifestResourceStream("Wabbajack.App.Wpf.LoginManagers.Icons.mega-text.png")!);

        this.WhenActivated(disposables =>
        {

            this.BindCommand(ViewModel, vm => vm.CloseCommand, v => v.CloseButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.LoginCommand, v => v.LoginButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.LoginAnonymouslyCommand, v => v.LoginAnonymouslyButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.TwoFactorLoginCommand, v => v.TwoFactorLoginButton)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.LoginSuccessful)
                     .Subscribe(success =>
                     {
                         if (success)
                         {
                             TwoFactorStatusBorder.Background = StatusBorder.Background = (SolidColorBrush)Application.Current.Resources["SuccessBrush"];
                             TwoFactorStatusIcon.Symbol = StatusIcon.Symbol = FluentIcons.Common.Symbol.CheckmarkCircle;
                             TwoFactorStatusIcon.Foreground = StatusIcon.Foreground = (SolidColorBrush)Application.Current.Resources["DarkBackgroundBrush"];
                             TwoFactorStatusText.Text = StatusText.Text = "Login successful!";
                             TwoFactorStatusText.Foreground = StatusText.Foreground = (SolidColorBrush)Application.Current.Resources["DarkBackgroundBrush"];
                         }
                         else
                         {
                             TwoFactorStatusBorder.Background = StatusBorder.Background = (SolidColorBrush)Application.Current.Resources["ErrorBrush"];
                             TwoFactorStatusIcon.Symbol = StatusIcon.Symbol = FluentIcons.Common.Symbol.DismissCircle;
                             TwoFactorStatusIcon.Foreground = StatusIcon.Foreground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"];
                             TwoFactorStatusText.Text = StatusText.Text = "Login failed! Please try again.";
                             TwoFactorStatusText.Foreground = StatusText.Foreground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"];
                         }
                     })
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.TriedLoggingIn, vm => vm.TwoFactorLoginRequested)
                     .ObserveOnGuiThread()
                     .Select(x => x.Item1 && !x.Item2 ? Visibility.Visible : Visibility.Collapsed)
                     .BindToStrict(this, v => v.StatusBorder.Visibility)
                     .DisposeWith(disposables);


            ViewModel.WhenAnyValue(vm => vm.TriedLoggingInWithTwoFactor)
                     .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                     .BindToStrict(this, v => v.TwoFactorStatusBorder.Visibility)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.TwoFactorLoginRequested)
                .Select(x => x ? Visibility.Collapsed : Visibility.Visible)
                .BindToStrict(this, v => v.LoginGrid.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.TwoFactorLoginRequested)
                .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                .BindToStrict(this, v => v.TwoFactorLoginGrid.Visibility)
                .DisposeWith(disposables);

            this.BindStrict(ViewModel, vm => vm.Email, v => v.EmailTextBox.Text)
                .DisposeWith(disposables);

            this.BindStrict(ViewModel, vm => vm.TwoFactorKey, v => v.TwoFactorKeyTextBox.Text)
                .DisposeWith(disposables);
        });
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;
        ViewModel.Password = password;
    }

    private void TwoFactor_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        Regex numericOnlyRegex = new Regex("[^0-9]+");
        e.Handled = numericOnlyRegex.IsMatch(e.Text);
    }
}

