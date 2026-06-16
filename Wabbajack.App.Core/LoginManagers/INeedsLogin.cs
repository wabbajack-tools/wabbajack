using System;
using System.Windows.Input;
using Wabbajack.Downloaders.Interfaces;

namespace Wabbajack.LoginManagers;

public interface INeedsLogin
{
    string SiteName { get; }
    ICommand TriggerLogin { get; set; }
    ICommand ClearLogin { get; set; }
    ICommand ToggleLogin { get; set; }
    object Icon { get; set; }
    Type LoginFor();
    public bool LoggedIn { get; set; }
}

public interface ILoginFor<T> : INeedsLogin
where T : IDownloader
{
}
