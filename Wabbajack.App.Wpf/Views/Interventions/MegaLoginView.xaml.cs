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

            ViewModel.WhenAnyValue(vm => vm.LoginSuccessful)
                     .Subscribe(success =>
                     {
                         if (success)
                         {
                             StatusBorder.Background = (SolidColorBrush)Application.Current.Resources["SuccessBrush"];
                             StatusIcon.Symbol = FluentIcons.Common.Symbol.CheckmarkCircle;
                             StatusIcon.Foreground = (SolidColorBrush)Application.Current.Resources["DarkBackgroundBrush"];
                             StatusText.Text = "Login successful!";
                             StatusText.Foreground = (SolidColorBrush)Application.Current.Resources["DarkBackgroundBrush"];
                         }
                         else
                         {
                             StatusBorder.Background = (SolidColorBrush)Application.Current.Resources["ErrorBrush"];
                             StatusIcon.Symbol = FluentIcons.Common.Symbol.DismissCircle;
                             StatusIcon.Foreground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"];
                             StatusText.Text = "Login failed! Please try again.";
                             StatusText.Foreground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"];
                         }
                     })
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.TriedLoggingIn)
                     .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                     .BindToStrict(this, v => v.StatusBorder.Visibility)
                     .DisposeWith(disposables);
        });
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;
        ViewModel.Password = password;
    }
}

