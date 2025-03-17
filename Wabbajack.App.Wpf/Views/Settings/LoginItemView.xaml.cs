using System;
using ReactiveUI;
using System.Reactive.Disposables;
using ReactiveMarbles.ObservableEvents;
using System.Reactive;
using System.Reactive.Linq;

namespace Wabbajack;

public partial class LoginItemView : IViewFor<LoginTargetVM>
{
    public LoginItemView()
    {
        InitializeComponent();
        this.WhenActivated(disposable =>
        {
            ViewModel.WhenAny(x => x.Login.Icon)
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

            LoginButton.Events().MouseEnter
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

            LoginButton.Events().MouseLeave
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
