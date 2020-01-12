using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ReactiveUI;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for LoginItemView.xaml
    /// </summary>
    public partial class LoginItemView : ReactiveUserControl<LoginTargetVM>
    {
        public LoginItemView()
        {
            InitializeComponent();
            this.WhenActivated(disposable =>
            {
                this.OneWayBindStrict(this.ViewModel, x => x.Login.SiteName, x => x.SiteNameText.Text)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(this.ViewModel, x => x.Login.TriggerLogin, x => x.LoginButton.Command)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(this.ViewModel, x => x.Login.ClearLogin, x => x.LogoutButton.Command)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(this.ViewModel, x => x.MetaInfo, x => x.MetaText.Text)
                    .DisposeWith(disposable);

                // Modify label state
                this.WhenAny(x => x.ViewModel.Login.TriggerLogin.CanExecute)
                    .Switch()
                    .Subscribe(x =>
                    {
                        this.LoginButton.Content = x ? "Login" : "Logged In";
                    });
                this.WhenAny(x => x.ViewModel.Login.ClearLogin.CanExecute)
                    .Switch()
                    .Subscribe(x =>
                    {
                        this.LogoutButton.Content = x ? "Logout" : "Logged Out";
                    });
            });
        }
    }
}
