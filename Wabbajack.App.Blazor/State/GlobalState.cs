using System;
using System.Windows.Shell;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.App.Blazor.State;

public class GlobalState
{
    #region Navigation Allowed

    private bool _navigationAllowed = true;

    public event Action OnNavigationStateChange;

    public bool NavigationAllowed
    {
        get => _navigationAllowed;
        set
        {
            _navigationAllowed = value;
            OnNavigationStateChange?.Invoke();
        }
    }

    #endregion

    #region Install

    private InstallStateEnum _installState;
    private AbsolutePath     _modListPath;
    private ModList          _modList;

    public event Action OnModListPathChange;
    public event Action OnModListChange;
    public event Action OnInstallStateChange;

    public event Action<TaskBarState> OnTaskBarStateChange;

    public void SetTaskBarState(TaskbarItemProgressState state = TaskbarItemProgressState.None, string description="",  double progress = 0)
    {
        OnTaskBarStateChange?.Invoke(new TaskBarState
        {
            State = state,
            ProgressValue = progress,
            Description = description
        });
    }

    public AbsolutePath ModListPath
    {
        get => _modListPath;
        set
        {
            _modListPath = value;
            OnModListPathChange?.Invoke();
        }
    }

    public ModList ModList
    {
        get => _modList;
        set
        {
            _modList = value;
            OnModListChange?.Invoke();
        }
    }

    public InstallStateEnum InstallState
    {
        get => _installState;
        set
        {
            _installState = value;
            OnInstallStateChange?.Invoke();
        }
    }

    public enum InstallStateEnum
    {
        Waiting,
        Configuration,
        Installing,
        Success,
        Failure
    }

    #endregion
}
