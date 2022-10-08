using System;
using System.Drawing;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Downloaders.IPS4OAuth2Downloader;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.UserIntervention;

namespace Wabbajack.LoginManagers;

public class VectorPlexusLoginManager : ViewModel, ILoginFor<LoversLabDownloader>
{
    private readonly ILogger<VectorPlexusLoginManager> _logger;
    private readonly ITokenProvider<VectorPlexusLoginState> _token;
    private readonly IServiceProvider _serviceProvider;

    public string SiteName { get; } = "Vector Plexus";
    public ICommand TriggerLogin { get; set; }
    public ICommand ClearLogin { get; set; }
    
    public ImageSource Icon { get; set; }
    public Type LoginFor()
    {
        return typeof(LoversLabDownloader);
    }

    [Reactive]
    public bool HaveLogin { get; set; }
    
    public VectorPlexusLoginManager(ILogger<VectorPlexusLoginManager> logger, ITokenProvider<VectorPlexusLoginState> token, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _token = token;
        _serviceProvider = serviceProvider;
        RefreshTokenState();
        
        ClearLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Deleting Login information for {SiteName}", SiteName);
            await _token.Delete();
            RefreshTokenState();
        }, this.WhenAnyValue(v => v.HaveLogin));

        Icon = BitmapFrame.Create(
            typeof(VectorPlexusLoginManager).Assembly.GetManifestResourceStream("Wabbajack.App.Wpf.LoginManagers.Icons.vector_plexus.png")!);
        
        TriggerLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Logging into {SiteName}", SiteName);
            StartLogin();
        }, this.WhenAnyValue(v => v.HaveLogin).Select(v => !v));
    }
    
        
    private void StartLogin()
    {
        var view = new BrowserWindow();
        view.Closed += (sender, args) => { RefreshTokenState(); };
        var provider = _serviceProvider.GetRequiredService<VectorPlexusLoginHandler>();
        view.DataContext = provider;
        view.Show();
    }


    private void RefreshTokenState()
    {
        HaveLogin = _token.HaveToken();
    }
}