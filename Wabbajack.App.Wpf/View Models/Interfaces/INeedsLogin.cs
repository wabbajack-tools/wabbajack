using System.Windows.Input;

namespace Wabbajack;

public interface INeedsLogin
{
    string SiteName { get; }
    ICommand TriggerLogin { get; set; }
    ICommand ClearLogin { get; set; }
    object? IconUri { get; set; }
}