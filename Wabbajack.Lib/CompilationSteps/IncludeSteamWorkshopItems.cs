using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
using Wabbajack.Common.StoreHandlers;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeSteamWorkshopItems : ACompilationStep
    {
        private readonly Regex _regex = new Regex("steamWorkshopItem_\\d*\\.meta$");
        private readonly bool _isGenericGame;
        private readonly SteamGame? _game;

        public IncludeSteamWorkshopItems(ACompiler compiler) : base(compiler)
        {
            var mo2Compiler = (MO2Compiler)compiler;
            _isGenericGame = mo2Compiler.CompilingGame.IsGenericMO2Plugin;
            var game = StoreHandler.Instance.SteamHandler.Games.FirstOrDefault(x =>
                x.Game == mo2Compiler.CompilingGame.Game);
            if (game != null)
                _game = (SteamGame)game;
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!_isGenericGame)
                return null;

            if (_game == null)
                return null;

            if (!_regex.IsMatch((string)source.Path)) 
                return null;

            try
            {
                var lines = await source.AbsolutePath.ReadAllLinesAsync();
                var list = lines.ToList();
                var sID = list.FirstOrDefault(l => l.StartsWith("itemID="))?.Replace("itemID=", "");
                if (string.IsNullOrEmpty(sID))
                {
                    Utils.Error($"Found no itemID= in file {source.AbsolutePath}!");
                    return null;
                }

                if(!int.TryParse(sID, out var id))
                {
                    Utils.Error($"Unable to parse int {sID} in {source.AbsolutePath}");
                    return null;
                }

                //Get-ChildItem -Name -Directory | ForEach-Object -Process {Out-File -FilePath .\steamWorkshopItem_$_.meta -InputObject "itemID=$($_)" -Encoding utf8}
                if (id == 0)
                    return null;

                SteamWorkshopItem? item = _game.WorkshopItems.FirstOrDefault(x => x.ItemID == id);
                if (item == null)
                {
                    Utils.Error($"Unable to find workshop item with ID {id} in loaded workshop item list!");
                    return null;
                }

                var fromSteam = source.EvolveTo<SteamMeta>();
                var str = list.Aggregate((x, y) => $"{x}\n{y}");
                if (!str.Contains("steamID="))
                    str += $"\nsteamID={item.SteamGameID}";
                var (sourceID, path) = await _compiler.IncludeString(str);
                fromSteam.SourceDataID = sourceID;
                fromSteam.Hash = await path.FileHashAsync();
                fromSteam.ItemSize = item.Size;
                fromSteam.ItemID = item.ItemID;
                return fromSteam;
            }
            catch (Exception e)
            {
                Utils.Error(e, $"Exception while trying to read {source.AbsolutePath}");
                return null;
            }
        }
    }
}
