using Wabbajack.DTOs;

namespace Wabbajack;

public class GameVM
{
    public Game Game { get; }
    public string DisplayName { get; }

    public GameVM(Game game)
    {
        Game = game;
        DisplayName = game.MetaData().HumanFriendlyGameName;
    }
}
