using System.Reactive.Disposables;
using System.Windows;
using ReactiveUI;
using Wabbajack.Lib.Downloaders;

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
                this.OneWayBind(ViewModel, x => x.ReturnMessage.Message, x => x.Message.Text)
                    .DisposeWith(disposable);
            });
        }

        private void LoginButton_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.Login(Password.SecurePassword);
        }
    }
}
