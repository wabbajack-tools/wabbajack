using Fluxor;
using Wabbajack.DTOs;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Blazor.Store;

[FeatureState]
public class DownloadState
{
    public DownloadStateEnum CurrentDownloadState    { get; }
    public ModlistMetadata   CurrentModlistMetadata  { get; }
    public Percent           CurrentDownloadProgress { get; }

    // Required for initial state.
    private DownloadState() { }

    public DownloadState(DownloadStateEnum newState, ModlistMetadata newModlist, Percent newProgress)
    {
        CurrentDownloadState    = newState;
        CurrentModlistMetadata  = newModlist;
        CurrentDownloadProgress = newProgress;
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
    public DownloadState.DownloadStateEnum State            { get; }
    public ModlistMetadata                 Modlist          { get; }
    public Percent                         DownloadProgress { get; }

    public UpdateDownloadState(DownloadState.DownloadStateEnum state, ModlistMetadata modlist, Percent? currentDownloadProgress)
    {
        State            = state;
        Modlist          = modlist;
        DownloadProgress = currentDownloadProgress ?? Percent.Zero;
    }
}

public static class DownloadStateReducers
{
    [ReducerMethod]
    public static DownloadState ReduceChangeDownloadState(DownloadState state, UpdateDownloadState action) =>
        new(action.State, action.Modlist, action.DownloadProgress);
}