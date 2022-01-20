using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Blazor.State;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Blazor.Pages;

public partial class Gallery
{
    [Inject] private GlobalState               GlobalState       { get; set; }
    [Inject] private NavigationManager         NavigationManager { get; set; }
    [Inject] private ILogger<Gallery>          _logger           { get; set; }
    [Inject] private Client                    _client           { get; set; }
    [Inject] private ModListDownloadMaintainer _maintainer       { get; set; }

    public Percent         DownloadProgress    { get; set; } = Percent.Zero;
    public ModlistMetadata DownloadingMetaData { get; set; } = new ModlistMetadata();

    private List<ModlistMetadata> _listItems { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _logger.LogInformation("Getting modlists...");
            ModlistMetadata[] modLists = await _client.LoadLists();
            _listItems.AddRange(modLists.ToList());
            StateHasChanged();
        }
        catch (Exception ex)
        {
            //TODO: [Critical] Figure out why an exception is thrown on first navigation.
            _logger.LogError(ex, "Error while loading lists");
        }

        await base.OnInitializedAsync();
    }

    private async void OnClickDownload(ModlistMetadata metadata)
    {
        // GlobalState.NavigationAllowed = !GlobalState.NavigationAllowed;
        await Download(metadata);
    }

    private async void OnClickInformation(ModlistMetadata metadata)
    {
        // TODO: [High] Implement information modal.
    }

    private async Task Download(ModlistMetadata metadata)
    {
        GlobalState.NavigationAllowed = false;
        DownloadingMetaData           = metadata;
        await using Timer timer = new(_ => InvokeAsync(StateHasChanged));
        timer.Change(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
        try
        {
            (IObservable<Percent> progress, Task task) = _maintainer.DownloadModlist(metadata);
            IDisposable dispose = progress.Subscribe(p => DownloadProgress = p);

            await task;
            //await _wjClient.SendMetric("downloading", Metadata.Title);
            dispose.Dispose();

            AbsolutePath path = KnownFolders.EntryPoint.Combine("downloaded_mod_lists", metadata.Links.MachineURL).WithExtension(Ext.Wabbajack);
            GlobalState.ModListPath = path;
            NavigationManager.NavigateTo("/Configure");
        }
        catch (Exception e)
        {
            Debug.Print(e.Message);
        }

        await timer.DisposeAsync();
        GlobalState.NavigationAllowed = true;
    }
}
