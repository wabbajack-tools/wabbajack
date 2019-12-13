using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeSteamWorkshopItems : ACompilationStep
    {
        private readonly SteamGame _game;
        private readonly Regex _regex = new Regex("steamWorkshopItem_\\d*\\.meta$");

        public IncludeSteamWorkshopItems(ACompiler compiler, SteamGame steamGame) : base(compiler)
        {
            _game = steamGame;
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            if (!_regex.IsMatch(source.Path)) 
                return null;

            try
            {
                var lines = File.ReadAllLines(source.AbsolutePath);
                var id = 0;
                lines.Where(l => l.StartsWith("itemID=")).Do(l => int.TryParse(l.Replace("itemID=", ""), out id));
                if (id == 0)
                    return null;

                SteamWorkshopItem item = null;
                _game.WorkshopItems.Where(i => i.ItemID == id).Do(i => item = i);
                if (item == null)
                    return null;

                var fromSteam = source.EvolveTo<SteamMeta>();
                fromSteam.SourceDataID = _compiler.IncludeFile(source.AbsolutePath);
                fromSteam.ItemID = item.ItemID;
                fromSteam.Size = item.Size;
                return fromSteam;
            }
            catch (Exception e)
            {
                Utils.Error(e, $"Exception while trying to evolve source to FromSteam");
                return null;
            }
        }

        public override IState GetState()
        {
            return new State(_game);
        }

        public class State : IState
        {
            private readonly SteamGame _game;

            public State(SteamGame game)
            {
                _game = game;
            }

            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeSteamWorkshopItems(compiler, _game);
            }
        }
    }
}
