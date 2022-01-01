using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack;
using Wabbajack.LoginManagers;

namespace Wabbajack
{
 
    public class LoginManagerVM : BackNavigatingVM
    {
        public LoginTargetVM[] Logins { get; }

        public LoginManagerVM(ILogger<LoginManagerVM> logger, SettingsVM settingsVM, IEnumerable<INeedsLogin> logins)
            : base(logger)
        {
            Logins = logins.Select(l => new LoginTargetVM(l)).ToArray();
        }
        
    }

    public class LoginTargetVM : ViewModel
    {
        public INeedsLogin Login { get; }
        public LoginTargetVM(INeedsLogin login)
        {
            Login = login;
        }
    }
    
}
