using System;
using System.Net.Mail;
using System.Reactive.Linq;
using System.Security;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
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
                .Select(IsValidAddress)
                .ToGuiProperty(this,
                    nameof(LoginEnabled));
        }

        public void Login(SecureString password)
        {
            try
            {
                if (password == null || password.Length == 0)
                {
                    ReturnMessage = new LoginReturnMessage("You need to input a password!", true);
                    return;
                }

                ReturnMessage = _downloader.LoginWithCredentials(Username, password);
                password.Clear();
            }
            catch (Exception e)
            {
                Utils.Error(e, "Exception while trying to login");
                ReturnMessage = new LoginReturnMessage($"Unhandled exception: {e.Message}", true);
            }
        }

        private static bool IsValidAddress(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return false;

            try
            {
                var _ = new MailAddress(s);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }
    }
}
