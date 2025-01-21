using System.Reactive.Disposables;
using ReactiveUI;

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

            this.BindCommand(ViewModel, vm => vm.Login.TriggerLogin, view => view.LoginButton)
                .DisposeWith(disposable);
            
            /*
            this.BindCommand(ViewModel, vm => vm.Login.ClearLogin, view => view.LogoutButton)
                .DisposeWith(disposable);

            */
        });
    }
}
