using ReactiveUI;

namespace Wabbajack.App.Avalonia.Messages;

/// <summary>Takes the nav rail away, as the installer does while preflight or an install is running.</summary>
public class HideNavigation
{
    public static void Send() => MessageBus.Current.SendMessage(new HideNavigation());
}

/// <summary>Puts the nav rail back.</summary>
public class ShowNavigation
{
    public static void Send() => MessageBus.Current.SendMessage(new ShowNavigation());
}

/// <summary>Asks the installer to load the modlist it last had, if the file is still there.</summary>
public class LoadLastLoadedModlist
{
    public static void Send() => MessageBus.Current.SendMessage(new LoadLastLoadedModlist());
}
