using System.Reactive.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.LoginManagers;

public class NexusLoginManager : ViewModel, INeedsLogin
{
    private readonly ILogger<NexusLoginManager> _logger;
    private readonly ITokenProvider<NexusApiState> _token;
    private readonly IUserInterventionHandler _handler;

    public string SiteName { get; } = "Nexus Mods";
    public ICommand TriggerLogin { get; set; }
    public ICommand ClearLogin { get; set; }
    
    public ImageSource Icon { get; set; }
    
    [Reactive]
    public bool HaveLogin { get; set; }
    
    public NexusLoginManager(ILogger<NexusLoginManager> logger, ITokenProvider<NexusApiState> token)
    {
        _logger = logger;
        _token = token;
        RefreshTokenState();
        
        ClearLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Deleting Login information for {SiteName}", SiteName);
            await _token.Delete();
            RefreshTokenState();
        }, this.WhenAnyValue(v => v.HaveLogin));

        Icon = BitmapFrame.Create(
            typeof(NexusLoginManager).Assembly.GetManifestResourceStream("Wabbajack.App.Wpf.LoginManagers.Icons.nexus.png")!);
        
        TriggerLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Logging into {SiteName}", SiteName);
            await NexusLogin.Send();
            RefreshTokenState();
        }, this.WhenAnyValue(v => v.HaveLogin).Select(v => !v));
    }

    private void RefreshTokenState()
    {
        HaveLogin = _token.HaveToken();
    }
}