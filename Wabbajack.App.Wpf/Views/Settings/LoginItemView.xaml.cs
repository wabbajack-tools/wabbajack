using System;
using System.Reactive.Disposables;
using ReactiveUI;

namespace Wabbajack
{
    public partial class LoginItemView
    {
        public LoginItemView()
        {
            InitializeComponent();
            this.WhenActivated(disposable =>
            {
                this.WhenAny(x => x.ViewModel.Login.Icon)
                    .BindToStrict(this, view => view.Favicon.Source)
                    .DisposeWith(disposable);
                
                this.OneWayBindStrict(ViewModel, x => x.Login.SiteName, x => x.SiteNameText.Text)
                    .DisposeWith(disposable);
                
                this.OneWayBindStrict(ViewModel, x => x.Login.TriggerLogin, x => x.LoginButton.Command)
                    .DisposeWith(disposable);

                this.OneWayBindStrict(ViewModel, x => x.Login.ClearLogin, x => x.LogoutButton.Command)
                    .DisposeWith(disposable);
            });
        }
    }
}
