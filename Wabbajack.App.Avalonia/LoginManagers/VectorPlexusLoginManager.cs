using Wabbajack.Views;
using System;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Common;
using Wabbajack.Downloaders.IPS4OAuth2Downloader;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.UserIntervention;

namespace Wabbajack.LoginManagers;

public partial class VectorPlexusLoginManager : ViewModel, ILoginFor<LoversLabDownloader>
{
    private readonly ILogger<VectorPlexusLoginManager> _logger;
    private readonly ITokenProvider<VectorPlexusLoginState> _token;
    private readonly IServiceProvider _serviceProvider;

    public string SiteName { get; } = "Vector Plexus";
    public ICommand TriggerLogin { get; set; }
    public ICommand ClearLogin { get; set; }
    public ICommand ToggleLogin { get; set; }
    
    public IImage Icon { get; set; }
    public Type LoginFor()
    {
        return typeof(LoversLabDownloader);
    }

    [Reactive]
    public partial bool LoggedIn { get; set; }
    
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
        }, this.WhenAnyValue(v => v.LoggedIn));

        Icon = new Bitmap(
            typeof(VectorPlexusLoginManager).Assembly.GetManifestResourceStream("Wabbajack.LoginManagers.Icons.vector_plexus.png")!);
        
        TriggerLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Logging into {SiteName}", SiteName);
            StartLogin();
        }, this.WhenAnyValue(v => v.LoggedIn).Select(v => !v));

        ToggleLogin = ReactiveCommand.Create(() =>
        {
            if (LoggedIn) ClearLogin.Execute(null);
            else TriggerLogin.Execute(null);
        });
    }
    
        
    private void StartLogin()
    {
        var browserView = _serviceProvider.GetRequiredService<BrowserWindow>();
        browserView.ViewModel.Closed += (_, _) => RefreshTokenState();
        ShowBrowserWindow.Send(_serviceProvider.GetRequiredService<VectorPlexusLoginHandler>());
    }


    private void RefreshTokenState()
    {
        LoggedIn = _token.HaveToken();
    }
}
