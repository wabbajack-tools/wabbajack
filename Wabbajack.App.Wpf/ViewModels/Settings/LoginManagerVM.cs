using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ReactiveUI.Fody.Helpers;
using Wabbajack.LoginManagers;

namespace Wabbajack;

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

