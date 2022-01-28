using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Shell;
using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Blazor.Components;
using Wabbajack.App.Blazor.State;
using Wabbajack.DTOs;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Blazor.Pages;

public partial class Gallery
{
    [Inject] private ILogger<Gallery> Logger { get; set; } = default!;
    [Inject] private IStateContainer StateContainer { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ModListDownloadMaintainer Maintainer { get; set; } = default!;
    [Inject] private IModalService Modal { get; set; } = default!;
    
    private IObservable<Percent>? DownloadProgress { get; set; }
    private ModlistMetadata? DownloadingMetaData { get; set; }

    private IEnumerable<ModlistMetadata> Modlists => StateContainer.Modlists;

    private bool _errorLoadingModlists;

    private bool _shouldRender;
    protected override bool ShouldRender() => _shouldRender;

    protected override async Task OnInitializedAsync()
    {
        if (!StateContainer.Modlists.Any())
        {
            var res = await StateContainer.LoadModlistMetadata();
            if (!res)
            {
                _errorLoadingModlists = true;
                _shouldRender = true;
                return;
            }
        }

        _shouldRender = true;
    }

    private async Task OnClickDownload(ModlistMetadata metadata)
    {
        if (!await Maintainer.HaveModList(metadata)) await Download(metadata);
        StateContainer.ModlistPath = Maintainer.ModListPath(metadata);
        StateContainer.Modlist = null;
        NavigationManager.NavigateTo(Configure.Route);
    }

    private void OnClickInformation(ModlistMetadata metadata)
    {
        // TODO: [High] Implement information modal.
        var parameters = new ModalParameters();
        parameters.Add(nameof(InfoModal.Content), metadata.Description);
        Modal.Show<InfoModal>("Information", parameters);
    }

    private async Task Download(ModlistMetadata metadata)
    {
        StateContainer.NavigationAllowed = false;
        DownloadingMetaData = metadata;

        try
        {
            var (progress, task) = Maintainer.DownloadModlist(metadata);

            DownloadProgress = progress;

            var dispose = progress
                .Sample(TimeSpan.FromMilliseconds(250))
                .DistinctUntilChanged(p => p.Value)
                .Subscribe(p => {
                    StateContainer.TaskBarState = new TaskBarState
                    {
                        Description = $"Downloading {metadata.Title}",
                        State = TaskbarItemProgressState.Normal,
                        ProgressValue = p.Value
                    };
                }, () => { StateContainer.TaskBarState = new TaskBarState(); });

            await task;
            dispose.Dispose();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Exception downloading Modlist {Name}", metadata.Title);
        }
        finally
        {
            StateContainer.TaskBarState = new TaskBarState();
            StateContainer.NavigationAllowed = true;
        }
    }
}
