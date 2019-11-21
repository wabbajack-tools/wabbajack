using Wabbajack.Common;

namespace Wabbajack
{
    public class GameVM
    {
        public Game Game { get; }
        public string DisplayName { get; }

        public GameVM(Game game)
        {
            this.Game = game;
            this.DisplayName = game.ToDescriptionString();
        }
    }
}
