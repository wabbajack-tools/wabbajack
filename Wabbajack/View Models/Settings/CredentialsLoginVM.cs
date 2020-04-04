using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack
{
    public class CredentialsLoginVM : ViewModel
    {
        [Reactive]
        public string Username { get; set; }

        [Reactive]
        public string Password { get; set; }

        [Reactive]
        public LoginReturnMessage ReturnMessage { get; set; }

        public ReactiveCommand<Unit, Unit> LoginCommand;

        public CredentialsLoginVM(INeedsLoginCredentials downloader)
        {
            LoginCommand = ReactiveCommand.Create(() =>
            {
                ReturnMessage = downloader.LoginWithCredentials(Username, Password);
                Password = "";
            }, this.WhenAny(x => x.Username).CombineLatest(this.WhenAny(x => x.Password),
                (username, password) => !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)));
        }
    }
}
