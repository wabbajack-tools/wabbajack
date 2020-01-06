using System;
using System.Collections.Generic;
using System.Linq;
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

        public class LoginTargetVM : ViewModel
        {
            private readonly ObservableAsPropertyHelper<string> _MetaInfo;
            public string MetaInfo => _MetaInfo.Value;

            public INeedsLogin Login { get; }

            public LoginTargetVM(INeedsLogin login)
            {
                Login = login;
                _MetaInfo = login.MetaInfo
                    .ToProperty(this, nameof(MetaInfo));
            }
        }
    }
}
