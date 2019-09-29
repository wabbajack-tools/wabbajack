using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Validation
{
    public class Permissions
    {
        public bool? CanExtractBSAs { get; set; }
        public bool? CanModifyESPs { get; set; }
        public bool? CanModifyAssets { get; set; }
        public bool? CanUseInOtherGames { get; set; }
    }

    public class Author
    {
        public Permissions Permissions { get; set; }
        public Dictionary<string, Game> Games;
    }

    public class Game
    {
        public Permissions Permissions;
        public Dictionary<string, Mod> Mods;
    }

    public class Mod
    {
        public Permissions Permissions;
        public Dictionary<string, File> Files;
    }

    public class File
    {
        public Permissions Permissions;
    }
}
