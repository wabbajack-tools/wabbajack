
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using ReactiveUI;
using Wabbajack.Downloaders.Interfaces;

namespace Wabbajack.LoginManagers;

public interface INeedsLogin
{
    string SiteName { get; }
    ICommand TriggerLogin { get; set; }
    ICommand ClearLogin { get; set; }
    ImageSource Icon { get; set; }
    Type LoginFor();
}

public interface ILoginFor<T> : INeedsLogin
where T : IDownloader
{

}