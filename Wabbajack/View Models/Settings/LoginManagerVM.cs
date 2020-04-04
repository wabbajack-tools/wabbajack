using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack
{
    public class LoginManagerVM : BackNavigatingVM
    {
        public List<LoginTargetVM> Downloaders { get; }

        public LoginManagerVM(SettingsVM settingsVM)
            : base(settingsVM.MWVM)
        {
            Downloaders = DownloadDispatcher.Downloaders
                .OfType<INeedsLogin>()
                .Select(x => new LoginTargetVM(x))
                .ToList();
        }
    }

    public class LoginTargetVM : ViewModel
    {
        private readonly ObservableAsPropertyHelper<string> _metaInfo;
        public string MetaInfo => _metaInfo.Value;

        public INeedsLogin Login { get; }
        public INeedsLoginCredentials LoginWithCredentials { get; }
        public bool UsesCredentials { get; }

        public ReactiveCommand<Unit, Unit> TriggerCredentialsLogin;

        public LoginTargetVM(INeedsLogin login)
        {
            Login = login;

            if (login is INeedsLoginCredentials loginWithCredentials)
            {
                UsesCredentials = true;
                LoginWithCredentials = loginWithCredentials;
            }

            _metaInfo = (login.MetaInfo ?? Observable.Return(""))
                .ToGuiProperty(this, nameof(MetaInfo));

            if (!UsesCredentials)
                return;

            TriggerCredentialsLogin = ReactiveCommand.Create(() =>
            {
                if (!(login is INeedsLoginCredentials))
                    return;

                var loginWindow = new LoginWindowView(LoginWithCredentials);
                loginWindow.Show();
            }, LoginWithCredentials.IsLoggedIn.Select(b => !b).ObserveOnGuiThread());
        }
    }
}
