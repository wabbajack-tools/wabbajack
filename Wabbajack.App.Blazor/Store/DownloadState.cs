using Fluxor;
using Wabbajack.DTOs;

namespace Wabbajack.App.Blazor.Store;

[FeatureState]
public class DownloadState
{
    public DownloadStateEnum CurrentDownloadState   { get; }
    public ModlistMetadata   CurrentModlistMetadata { get; }

    // Required for initial state.
    private DownloadState() { }

    public DownloadState(DownloadStateEnum newState, ModlistMetadata newModlist)
    {
        CurrentDownloadState   = newState;
        CurrentModlistMetadata = newModlist;
    }

    public enum DownloadStateEnum
    {
        Waiting,
        Downloading,
        Downloaded,
        Failure
    }
}

public class UpdateDownloadState
{
    public DownloadState.DownloadStateEnum State   { get; }
    public ModlistMetadata                 Modlist { get; }

    public UpdateDownloadState(DownloadState.DownloadStateEnum state, ModlistMetadata modlist)
    {
        State   = state;
        Modlist = modlist;
    }
}

public static class DownloadStateReducers
{
    [ReducerMethod]
    public static DownloadState ReduceChangeDownloadState(DownloadState state, UpdateDownloadState action) =>
        new(action.State, action.Modlist);
}