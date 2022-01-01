using System;
using System.Reactive.Disposables;
using System.Windows.Forms;
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
                ViewModel.WhenAny(x => x.Login.Icon)
                    .BindToStrict(this, view => view.Favicon.Source)
                    .DisposeWith(disposable);

                ViewModel.WhenAnyValue(vm => vm.Login.SiteName)
                    .BindToStrict(this, view => view.SiteNameText.Text)
                    .DisposeWith(disposable);

                ViewModel.WhenAnyValue(vm => vm.Login.TriggerLogin)
                    .BindToStrict(this, view => view.LoginButton.Command)
                    .DisposeWith(disposable);
                
                ViewModel.WhenAnyValue(vm => vm.Login.ClearLogin)
                    .BindToStrict(this, view => view.LogoutButton.Command)
                    .DisposeWith(disposable);

            });
        }
    }
}
