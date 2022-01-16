using Fluxor;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.App.Blazor.Store;

[FeatureState]
public class InstallState
{
    public InstallStateEnum CurrentInstallState { get; }
    public ModList?         CurrentModList      { get; }
    public AbsolutePath?    CurrentModListPath  { get; }
    public AbsolutePath?    CurrentInstallPath  { get; }
    public AbsolutePath?    CurrentDownloadPath { get; }

    // Required for initial state.
    private InstallState() { }

    public InstallState(InstallStateEnum newState, ModList? newModList, AbsolutePath? newModListPath, AbsolutePath? newInstallPath, AbsolutePath? newDownloadPath)
    {
        CurrentInstallState = newState;
        CurrentModList      = newModList ?? CurrentModList;
        CurrentModListPath  = newModListPath ?? CurrentModListPath;
        CurrentInstallPath  = newInstallPath ?? CurrentInstallPath;
        CurrentDownloadPath  = newDownloadPath ?? CurrentDownloadPath;
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
    public InstallState.InstallStateEnum State        { get; }
    public ModList?                      ModList      { get; }
    public AbsolutePath?                 ModListPath  { get; }
    public AbsolutePath?                 InstallPath  { get; }
    public AbsolutePath?                 DownloadPath { get; }

    public UpdateInstallState(InstallState.InstallStateEnum state, ModList? modlist, AbsolutePath? modlistPath, AbsolutePath? installPath, AbsolutePath? downloadPath)
    {
        State        = state;
        ModList      = modlist;
        ModListPath  = modlistPath;
        InstallPath  = installPath;
        DownloadPath = downloadPath;
    }
}

public static class InstallStateReducers
{
    [ReducerMethod]
    public static InstallState ReduceChangeInstallState(InstallState state, UpdateInstallState action) => new(action.State, action.ModList, action.ModListPath, action.InstallPath, action.DownloadPath);
}