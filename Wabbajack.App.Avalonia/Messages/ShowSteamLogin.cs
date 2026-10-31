using System.Threading.Tasks;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Messages;

/// <summary>
///     Puts the Steam login pane in front of the user, as a floating pane over whatever they were doing: the
///     app already dims what is behind and closes on Escape.
/// </summary>
public class ShowSteamLogin
{
    private ShowSteamLogin(SteamLoginVM viewModel) => ViewModel = viewModel;

    public SteamLoginVM ViewModel { get; }

    /// <summary>
    ///     Shows the pane and completes with whether the account ended up logged in. The caller owns the view
    ///     model; the main window only borrows it while it is on screen.
    /// </summary>
    public static Task<bool> Send(SteamLoginVM viewModel)
    {
        MessageBus.Current.SendMessage(new ShowSteamLogin(viewModel));
        return viewModel.Result;
    }
}
