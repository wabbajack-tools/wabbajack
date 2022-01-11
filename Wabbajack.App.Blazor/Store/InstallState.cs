using Fluxor;
using Wabbajack.DTOs;

namespace Wabbajack.App.Blazor.Store;

[FeatureState]
public class InstallState
{
    public InstallStateEnum CurrentInstallState    { get; }
    public ModlistMetadata? CurrentModlistMetadata { get; }

    // Required for initial state.
    private InstallState() { }

    public InstallState(InstallStateEnum newState, ModlistMetadata? newModlist)
    {
        CurrentInstallState    = newState;
        CurrentModlistMetadata = newModlist;
    }
    

    public enum InstallStateEnum
    {
        Configuration,
        Installing,
        Success,
        Failure
    }
}

public class UpdateInstallState
{
    public InstallState.InstallStateEnum State   { get; }
    public ModlistMetadata?              Modlist { get; }

    public UpdateInstallState(InstallState.InstallStateEnum state, ModlistMetadata? modlist = null)
    {
        State   = state;
        Modlist = modlist;
    }
}

public static class InstallStateReducers
{
    [ReducerMethod]
    public static InstallState ReduceChangeInstallState(InstallState state, UpdateInstallState action) => new(action.State, action.Modlist);
}