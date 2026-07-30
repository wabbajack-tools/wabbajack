using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.LoginManagers;

namespace Wabbajack;

public partial class LoginItemView : ReactiveUserControl<LoginTargetVM>
{
    public LoginItemView()
    {
        InitializeComponent();
        this.WhenActivated(disposable =>
        {
            // TODO: LoginTargetVM.Login.Icon (INeedsLogin.Icon) is currently typed as
            // System.Windows.Media.ImageSource (WPF-specific). Avalonia's Image.Source
            // expects Avalonia.Media.IImage (e.g. Avalonia.Media.Imaging.Bitmap). The
            // binding below is a structurally faithful port of the WPF code but will need
            // INeedsLogin/LoginTargetVM updated to expose an Avalonia-compatible image type
            // before this compiles/works; that interface/VM change is out of scope for this
            // view-only port.
            ViewModel.WhenAnyValue(x => x.Login.Icon)
                .BindToStrict(this, view => view.Favicon.Source)
                .DisposeWith(disposable);

            ViewModel.WhenAnyValue(vm => vm.Login.SiteName)
                .BindToStrict(this, view => view.SiteNameText.Text)
                .DisposeWith(disposable);

            this.BindCommand(ViewModel, vm => vm.Login.ToggleLogin, view => view.LoginButton)
                .DisposeWith(disposable);

            ViewModel.WhenAnyValue(vm => vm.Login.LoggedIn)
                     .ObserveOnGuiThread()
                     .Subscribe(loggedIn =>
                     {
                         if (loggedIn)
                         {
                             LoginButton.Text = "Logged in";
                             LoginButton.Icon = FluentIcons.Common.Symbol.PersonAvailable;
                             LoginButton.IconVariant = FluentIcons.Common.IconVariant.Filled;
                             LoginButton.ButtonStyle = ButtonStyle.Color;
                         }
                         else
                         {
                             LoginButton.Text = "Log in";
                             LoginButton.Icon = FluentIcons.Common.Symbol.PersonArrowRight;
                             LoginButton.IconVariant = FluentIcons.Common.IconVariant.Regular;
                             LoginButton.ButtonStyle = ButtonStyle.Mono;
                         }
                     })
                     .DisposeWith(disposable);

            // WPF: LoginButton.Events().MouseEnter -> Avalonia: PointerEntered on the WJButton.
            // ReactiveMarbles.ObservableEvents doesn't expose WPF's MouseEnter/MouseLeave names
            // on Avalonia controls, so the equivalent pointer events are wired up directly via
            // Observable.FromEventPattern instead of the Events() helper.
            Observable.FromEventPattern<PointerEventArgs>(
                          h => LoginButton.PointerEntered += h,
                          h => LoginButton.PointerEntered -= h)
                      .ObserveOnGuiThread()
                      .Subscribe(_ =>
                      {
                          if (ViewModel.Login.LoggedIn)
                          {
                              LoginButton.Text = "Log out";
                              LoginButton.Icon = FluentIcons.Common.Symbol.PersonArrowLeft;
                              LoginButton.IconVariant = FluentIcons.Common.IconVariant.Regular;
                          }
                      })
                      .DisposeWith(disposable);

            // WPF: LoginButton.Events().MouseLeave -> Avalonia: PointerExited on the WJButton.
            Observable.FromEventPattern<PointerEventArgs>(
                          h => LoginButton.PointerExited += h,
                          h => LoginButton.PointerExited -= h)
                      .ObserveOnGuiThread()
                      .Subscribe(_ =>
                      {
                          if (ViewModel.Login.LoggedIn)
                          {
                              LoginButton.Text = "Logged in";
                              LoginButton.Icon = FluentIcons.Common.Symbol.PersonAvailable;
                              LoginButton.IconVariant = FluentIcons.Common.IconVariant.Filled;
                          }
                      })
                      .DisposeWith(disposable);

            /*
            this.BindCommand(ViewModel, vm => vm.Login.ClearLogin, view => view.LogoutButton)
                .DisposeWith(disposable);

            */
        });
    }
}
