using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.ViewModels;
using Wabbajack.DTOs;

namespace Wabbajack.App.Controls;

public class GameSelectorItemViewModel : ViewModelBase, IActivatableViewModel
{
    public GameSelectorItemViewModel(Game game)
    {
        Activator = new ViewModelActivator();
        Game = game;
        Name = game.MetaData().HumanFriendlyGameName;
    }

    [Reactive] public Game Game { get; set; }

    [Reactive] public string Name { get; set; }
}