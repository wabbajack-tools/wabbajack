using System.Reactive.Disposables;
using System.Windows;
using ReactiveUI;

namespace Wabbajack
{
    public partial class CredentialsLoginView
    {
        public INeedsLoginCredentials Downloader { get; set; }

        public CredentialsLoginView(INeedsLoginCredentials downloader)
        {
            Downloader = downloader;

            if (ViewModel == null)
                ViewModel = new CredentialsLoginVM(downloader);

            InitializeComponent();

            this.WhenActivated(disposable =>
            {
                this.Bind(ViewModel, x => x.Username, x => x.Username.Text)
                    .DisposeWith(disposable);
                this.Bind(ViewModel, x => x.LoginEnabled, x => x.LoginButton.IsEnabled)
                    .DisposeWith(disposable);
                this.Bind(ViewModel, x => x.MFAKey, x => x.MFA.Text)
                    .DisposeWith(disposable);
                this.OneWayBind(ViewModel, x => x.MFAVisible, x => x.MFA.Visibility)
                    .DisposeWith(disposable);
                this.OneWayBind(ViewModel, x => x.MFAVisible, x => x.MFAText.Visibility)
                    .DisposeWith(disposable);
                /* TODO
                this.OneWayBind(ViewModel, x => x.ReturnMessage.Message, x => x.Message.Text)
                    .DisposeWith(disposable);
                    */
            });
        }

        private async void LoginButton_OnClick(object sender, RoutedEventArgs e)
        {
            //ViewModel.Login(Password.SecurePassword).FireAndForget();
        }
    }
}
