using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            if (string.IsNullOrWhiteSpace(this.DisplayName))
            {
                this.DisplayName = game.ToString();
            }
        }
    }
}
