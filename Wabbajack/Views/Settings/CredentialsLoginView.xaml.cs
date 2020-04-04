using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
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
                this.Bind(ViewModel, x => x.Password, x => x.Password.Text)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(ViewModel, x => x.LoginCommand, x => x.LoginButton.Command)
                    .DisposeWith(disposable);
                this.OneWayBind(ViewModel, x => x.ReturnMessage.Message, x => x.Message.Text)
                    .DisposeWith(disposable);
            });
        }
    }
}
