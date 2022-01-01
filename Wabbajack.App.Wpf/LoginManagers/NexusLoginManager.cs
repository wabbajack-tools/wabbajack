using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.LoginManagers;

public class NexusLoginManager : INeedsLogin
{
    private readonly ILogger<NexusLoginManager> _logger;
    private readonly ITokenProvider<NexusApiState> _token;
    private readonly IUserInterventionHandler _handler;

    public string SiteName { get; } = "Nexus Mods";
    public ICommand TriggerLogin { get; set; }
    public ICommand ClearLogin { get; set; }
    
    public ImageSource Icon { get; set; }
    
    public NexusLoginManager(ILogger<NexusLoginManager> logger, ITokenProvider<NexusApiState> token)
    {
        _logger = logger;
        _token = token;

        ClearLogin = ReactiveCommand.Create(() =>
        {
            _logger.LogInformation("Deleting Login information for {SiteName}", SiteName);
            _token.Delete();
        });

        Icon = BitmapFrame.Create(
            typeof(NexusLoginManager).Assembly.GetManifestResourceStream("Wabbajack.LoginManagers.Icons.nexus.png")!);
        TriggerLogin = ReactiveCommand.Create(() =>
        {
            _logger.LogInformation("Logging into {SiteName}", SiteName);
            NexusLogin.Send();
        });
    }
}