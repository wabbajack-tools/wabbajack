using System.Reactive;
using System.Reactive.Linq;
using System.Security;
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
        public LoginReturnMessage ReturnMessage { get; set; }

        private readonly ObservableAsPropertyHelper<bool> _loginEnabled;
        public bool LoginEnabled => _loginEnabled.Value;

        private readonly INeedsLoginCredentials _downloader;

        public CredentialsLoginVM(INeedsLoginCredentials downloader)
        {
            _downloader = downloader;

            _loginEnabled = this.WhenAny(x => x.Username)
                .Select(username => !string.IsNullOrWhiteSpace(username))
                .ToGuiProperty(this,
                    nameof(LoginEnabled));
        }

        public void Login(SecureString password)
        {
            ReturnMessage = _downloader.LoginWithCredentials(Username, password);
            password.Clear();
        }
    }
}
