using System;
using System.Drawing;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.LoginManagers;

public class VectorPlexusLoginManager : ViewModel, INeedsLogin
{
    private readonly ILogger<VectorPlexusLoginManager> _logger;
    private readonly ITokenProvider<VectorPlexusLoginState> _token;
    private readonly IUserInterventionHandler _handler;

    public string SiteName { get; } = "Vector Plexus";
    public ICommand TriggerLogin { get; set; }
    public ICommand ClearLogin { get; set; }
    
    public ImageSource Icon { get; set; }
    
    [Reactive]
    public bool HaveLogin { get; set; }
    
    public VectorPlexusLoginManager(ILogger<VectorPlexusLoginManager> logger, ITokenProvider<VectorPlexusLoginState> token)
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
            typeof(NexusLoginManager).Assembly.GetManifestResourceStream("Wabbajack.LoginManagers.Icons.nexus.png")!);
        
        TriggerLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Logging into {SiteName}", SiteName);
            await VectorPlexusLogin.Send();
            RefreshTokenState();
        }, this.WhenAnyValue(v => v.HaveLogin).Select(v => !v));
    }

    private void RefreshTokenState()
    {
        HaveLogin = _token.HaveToken();
    }
}