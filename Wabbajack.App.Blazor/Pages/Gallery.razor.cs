using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Shell;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Blazor.State;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Blazor.Pages;

public partial class Gallery
{
    [Inject] private ILogger<Gallery> Logger { get; set; } = default!;
    [Inject] private IStateContainer StateContainer { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ModListDownloadMaintainer Maintainer { get; set; } = default!;
    [Inject] private Client Client { get; set; } = default!;

    private bool ShowingGameFilter { get; set; }
    private bool ShowingSourceFilter { get; set; }

    private Dictionary<string, bool> GameFilter { get; set; } = new();
    private string RepoFilter { get; set; } = "";

    private IObservable<Percent>? DownloadProgress { get; set; }
    private ModlistMetadata? DownloadingMetaData { get; set; }

    private IEnumerable<string> Repositories => Modlists.GroupBy(x => x.RepositoryName).Select(r => r.Key);
    private IEnumerable<GameMetaData> Games => Modlists.GroupBy(x => x.Game.MetaData()).Select(r => r.Key);

    private IEnumerable<ModlistMetadata> Modlists => StateContainer.Modlists;
    private IEnumerable<ModlistMetadata> FilteredModlists => GetFilteredModLists();

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

    private IEnumerable<ModlistMetadata> GetFilteredModLists()
    {
        IEnumerable<ModlistMetadata> filtered = Modlists;
        if (GameFilter.Values.Any(v => v)) filtered = filtered.Where(x => GameFilter.ContainsKey(x.Game.ToString()) && GameFilter[x.Game.ToString()]);
        filtered = filtered.Where(x => x.RepositoryName.Contains(RepoFilter, StringComparison.InvariantCultureIgnoreCase));
        return filtered;
    }

    private void UpdateGameFilter(string game, bool toggle = false)
    {
        if (!GameFilter.ContainsKey(game)) GameFilter[game] = false;

        if (toggle) GameFilter[game] = !GameFilter[game];
    }

    private void ToggleGameFilter() => ShowingGameFilter = !ShowingGameFilter;
    private void ToggleSourceFilter() => ShowingSourceFilter = !ShowingSourceFilter;

    private async Task OnClickDownload(ModlistMetadata metadata)
    {
        if (!await Maintainer.HaveModList(metadata)) await Download(metadata);
        StateContainer.ModlistPath = Maintainer.ModListPath(metadata);
        StateContainer.Modlist = null;
        NavigationManager.NavigateTo(Configure.Route);
    }

    // private async Task OnClickInformation(ModlistMetadata metadata)
    // {
    //     var detailedStatus = await Client.GetDetailedStatus(metadata.Links.MachineURL);
    //
    //     var parameters = new ModalParameters();
    //     parameters.Add(nameof(InfoModal.Content), metadata.Description);
    //     parameters.Add(nameof(InfoModal.ValidatedModList), detailedStatus);
    //     var options = new ModalOptions
    //     {
    //         Class = "list-info-modal",
    //         HideHeader = true,
    //         ContentScrollable = true
    //     };
    //     Modal.Show<InfoModal>("Information", parameters, options);
    // }

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
