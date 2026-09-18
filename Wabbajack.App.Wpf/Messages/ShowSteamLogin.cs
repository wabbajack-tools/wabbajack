using System.Threading.Tasks;
using ReactiveUI;

namespace Wabbajack.Messages;

/// <summary>
///     Puts the Steam login pane in front of the user. A floating pane rather than a window of its own: it is
///     always something they asked for in the middle of something else - a modlist that needs game files, the
///     Logins settings tile - and the app already dims what is behind and closes on Escape.
/// </summary>
public class ShowSteamLogin
{
    private ShowSteamLogin(SteamLoginVM viewModel)
    {
        ViewModel = viewModel;
    }

    public SteamLoginVM ViewModel { get; }

    /// <summary>
    ///     Shows the pane and completes with whether the account ended up logged in. The caller owns the view
    ///     model; the main window only borrows it for as long as it is on screen.
    /// </summary>
    public static Task<bool> Send(SteamLoginVM viewModel)
    {
        MessageBus.Current.SendMessage(new ShowSteamLogin(viewModel));
        return viewModel.Result;
    }
}
