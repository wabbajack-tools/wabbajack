using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Wabbajack.App.Blazor.Components;
using Wabbajack.App.Models;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Blazor.Pages;

public partial class Settings
{
    [Inject] private ResourceSettingsManager _resourceSettingsManager { get; set; }
    [Inject] private EncryptedJsonTokenProvider<NexusApiState> _nexusTokenProvider { get; set; }
    [Inject] private EncryptedJsonTokenProvider<LoversLabLoginState> _loversLabTokenProvider { get; set; }
    [Inject] private NexusApi _api { get; set; }

    private SiteLoginStatus NexusMods { get; } = new("Nexus Mods", null, SiteLogin.SiteStatus.Loading);
    private SiteLoginStatus LoversLab { get; } = new("Lovers Lab", null, SiteLogin.SiteStatus.Loading);
    private SiteLoginStatus VectorPlexus { get; } = new("Vector Plexus", null, SiteLogin.SiteStatus.Loading);

    public class SiteLoginStatus
    {
        public string SiteName;
        public string? UserName;
        public SiteLogin.SiteStatus Status;
        public SiteLoginStatus(string siteName, string? userName, SiteLogin.SiteStatus status)
        {
            SiteName = siteName;
            UserName = userName;
            Status = status;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        if (_nexusTokenProvider.HaveToken())
        {
            var validation = await _api.Validate();
            NexusMods.UserName = validation.info.Name;
            NexusMods.Status = SiteLogin.SiteStatus.LoggedIn;
        }

        LoversLab.Status = _loversLabTokenProvider.HaveToken() ? SiteLogin.SiteStatus.LoggedIn : SiteLogin.SiteStatus.LoggedOut;

        try
        {
            var resource = await _resourceSettingsManager.GetSettings("Downloads");
            StateHasChanged();
        }
        catch (Exception ex) {}

        StateHasChanged();

        await base.OnInitializedAsync();
    }
}
