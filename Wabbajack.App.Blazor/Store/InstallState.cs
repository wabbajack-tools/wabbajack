using Fluxor;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.App.Blazor.Store;

[FeatureState]
public class InstallState
{
    public InstallStateEnum CurrentInstallState { get; }
    public ModList?         CurrentModlist      { get; }
    public AbsolutePath?    CurrentPath         { get; }

    // Required for initial state.
    private InstallState() { }

    public InstallState(InstallStateEnum newState, ModList? newModList, AbsolutePath? newPath)
    {
        CurrentInstallState = newState;
        CurrentModlist      = newModList ?? CurrentModlist;
        CurrentPath         = newPath ?? CurrentPath;
    }

    public enum InstallStateEnum
    {
        Waiting,
        Configuration,
        Installing,
        Success,
        Failure
    }
}

public class UpdateInstallState
{
    public InstallState.InstallStateEnum State   { get; }
    public ModList?                      Modlist { get; }
    public AbsolutePath?                 Path    { get; }

    public UpdateInstallState(InstallState.InstallStateEnum state, ModList? modlist, AbsolutePath? path)
    {
        State   = state;
        Modlist = modlist;
        Path    = path;
    }
}

public static class InstallStateReducers
{
    [ReducerMethod]
    public static InstallState ReduceChangeInstallState(InstallState state, UpdateInstallState action) => new(action.State, action.Modlist, action.Path);
}