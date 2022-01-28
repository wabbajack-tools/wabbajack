using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;

namespace Wabbajack.App.Blazor.State;

public class StateContainer : IStateContainer
{
    private readonly ILogger<StateContainer> _logger;
    private readonly Client _client;

    public StateContainer(ILogger<StateContainer> logger, Client client)
    {
        _logger = logger;
        _client = client;
    }
    
    private ModlistMetadata[] _modlists = Array.Empty<ModlistMetadata>();
    public IEnumerable<ModlistMetadata> Modlists => _modlists;
    
    public async Task<bool> LoadModlistMetadata()
    {
        try
        {
            var lists = await _client.LoadLists();
            _modlists = lists;
            return _modlists.Any();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception loading Modlists");
            return false;
        }
    }

    private readonly CustomObservable<bool> _navigationAllowedObservable = new(true);
    public IObservable<bool> NavigationAllowedObservable => _navigationAllowedObservable;
    public bool NavigationAllowed
    {
        get => _navigationAllowedObservable.Value;
        set => _navigationAllowedObservable.Value = value;
    }

    private readonly CustomObservable<AbsolutePath> _modlistPathObservable = new(AbsolutePath.Empty);
    public IObservable<AbsolutePath> ModlistPathObservable => _modlistPathObservable;
    public AbsolutePath ModlistPath
    {
        get => _modlistPathObservable.Value;
        set => _modlistPathObservable.Value = value;
    }
    
    private readonly CustomObservable<AbsolutePath> _installPathObservable = new(AbsolutePath.Empty);
    public IObservable<AbsolutePath> InstallPathObservable => _installPathObservable;
    public AbsolutePath InstallPath
    {
        get => _installPathObservable.Value;
        set => _installPathObservable.Value = value;
    }
    
    private readonly CustomObservable<AbsolutePath> _downloadPathObservable = new(AbsolutePath.Empty);
    public IObservable<AbsolutePath> DownloadPathObservable => _downloadPathObservable;
    public AbsolutePath DownloadPath
    {
        get => _downloadPathObservable.Value;
        set => _downloadPathObservable.Value = value;
    }
    
    private readonly CustomObservable<ModList?> _modlistObservable = new(null);
    public IObservable<ModList?> ModlistObservable => _modlistObservable;
    public ModList? Modlist
    {
        get => _modlistObservable.Value;
        set => _modlistObservable.Value = value;
    }
    
    private readonly CustomObservable<string?> _modlistImageObservable = new(string.Empty);
    public IObservable<string?> ModlistImageObservable => _modlistImageObservable;
    public string? ModlistImage
    {
        get => _modlistImageObservable.Value;
        set => _modlistImageObservable.Value = value;
    }

    private readonly CustomObservable<InstallState> _installStateObservable = new(InstallState.Waiting);
    public IObservable<InstallState> InstallStateObservable => _installStateObservable;
    public InstallState InstallState
    {
        get => _installStateObservable.Value;
        set => _installStateObservable.Value = value;
    }

    private readonly CustomObservable<TaskBarState> _taskBarStateObservable = new(new TaskBarState());
    public IObservable<TaskBarState> TaskBarStateObservable => _taskBarStateObservable;
    public TaskBarState TaskBarState
    {
        get => _taskBarStateObservable.Value;
        set => _taskBarStateObservable.Value = value;
    }
}
