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

/// <summary>
///     Shows the info screen with a message and where its Back button returns to. Nothing in the WPF app sends
///     this, so the screen is only reachable through it.
/// </summary>
public class LoadInfoScreen(string info, Wabbajack.App.Avalonia.ViewModels.ViewModel navigateBackTarget)
{
    public string Info { get; } = info;
    public Wabbajack.App.Avalonia.ViewModels.ViewModel NavigateBackTarget { get; } = navigateBackTarget;

    public static void Send(Wabbajack.App.Avalonia.Services.Navigator navigator, string info,
        Wabbajack.App.Avalonia.ViewModels.ViewModel navigateBackTarget)
    {
        navigator.NavigateTo(Wabbajack.App.Avalonia.Services.ScreenType.Info);
        MessageBus.Current.SendMessage(new LoadInfoScreen(info, navigateBackTarget));
    }
}
