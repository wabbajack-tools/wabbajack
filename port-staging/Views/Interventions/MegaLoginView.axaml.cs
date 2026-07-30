using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.LoginManagers;

namespace Wabbajack;

public partial class MegaLoginView : ReactiveUserControl<MegaLoginVM>
{
    public MegaLoginView()
    {
        InitializeComponent();

        // TODO: Preserves the WPF behavior of loading the icon from an embedded manifest resource
        // stream on the assembly that owns MegaLoginManager. If this asset is instead published as
        // an Avalonia asset (AvaloniaResource) in the ported project, prefer:
        //   MegaImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://<AssemblyName>/LoginManagers/Icons/mega-text.png")));
        using (var stream = typeof(MegaLoginManager).Assembly.GetManifestResourceStream("Wabbajack.App.Wpf.LoginManagers.Icons.mega-text.png"))
        {
            MegaImage.Source = new Bitmap(stream!);
        }

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
                             TwoFactorStatusBorder.Background = StatusBorder.Background = (IBrush)this.FindResource("SuccessBrush")!;
                             TwoFactorStatusIcon.Symbol = StatusIcon.Symbol = FluentIcons.Common.Symbol.CheckmarkCircle;
                             TwoFactorStatusIcon.Foreground = StatusIcon.Foreground = (IBrush)this.FindResource("DarkBackgroundBrush")!;
                             TwoFactorStatusText.Text = StatusText.Text = "Login successful!";
                             TwoFactorStatusText.Foreground = StatusText.Foreground = (IBrush)this.FindResource("DarkBackgroundBrush")!;
                         }
                         else
                         {
                             TwoFactorStatusBorder.Background = StatusBorder.Background = (IBrush)this.FindResource("ErrorBrush")!;
                             TwoFactorStatusIcon.Symbol = StatusIcon.Symbol = FluentIcons.Common.Symbol.DismissCircle;
                             TwoFactorStatusIcon.Foreground = StatusIcon.Foreground = (IBrush)this.FindResource("ForegroundBrush")!;
                             TwoFactorStatusText.Text = StatusText.Text = "Login failed! Please try again.";
                             TwoFactorStatusText.Foreground = StatusText.Foreground = (IBrush)this.FindResource("ForegroundBrush")!;
                         }
                     })
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.TriedLoggingIn, vm => vm.TwoFactorLoginRequested)
                     .ObserveOnGuiThread()
                     .Select(x => x.Item1 && !x.Item2)
                     .BindToStrict(this, v => v.StatusBorder.IsVisible)
                     .DisposeWith(disposables);


            ViewModel.WhenAnyValue(vm => vm.TriedLoggingInWithTwoFactor)
                     .Select(x => x)
                     .BindToStrict(this, v => v.TwoFactorStatusBorder.IsVisible)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.TwoFactorLoginRequested)
                .Select(x => !x)
                .BindToStrict(this, v => v.LoginGrid.IsVisible)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.TwoFactorLoginRequested)
                .Select(x => x)
                .BindToStrict(this, v => v.TwoFactorLoginGrid.IsVisible)
                .DisposeWith(disposables);

            this.BindStrict(ViewModel, vm => vm.Email, v => v.EmailTextBox.Text)
                .DisposeWith(disposables);

            this.BindStrict(ViewModel, vm => vm.TwoFactorKey, v => v.TwoFactorKeyTextBox.Text)
                .DisposeWith(disposables);
        });
    }

    private void PasswordBox_PasswordChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        var password = PasswordBox.Text;
        ViewModel.Password = password ?? string.Empty;
    }

    private void TwoFactor_PreviewTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
            return;

        System.Text.RegularExpressions.Regex numericOnlyRegex = new System.Text.RegularExpressions.Regex("[^0-9]+");
        e.Handled = numericOnlyRegex.IsMatch(e.Text);
    }
}
