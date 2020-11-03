using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.CLI.Verbs
{
    [Verb("hash-game-files", HelpText = "Hashes a game's files for inclusion in the public github repo")]
    public class HashGamefiles : AVerb
    {
        [Option('o', "output", Required = true, HelpText = @"Output folder in which the file will be placed")]
        public string OutputFolder { get; set; } = "";

        private AbsolutePath _outputFolder => (AbsolutePath)OutputFolder;
        
        [Option('g', "game", Required = true, HelpText = @"WJ Game to index")]
        public string Game { get; set; } = "";

        private Game _game => GameRegistry.GetByFuzzyName(Game).Game;

        protected override async Task<ExitCode> Run()
        {
            var version = _game.MetaData().InstalledVersion;
            var file = _outputFolder.Combine(_game.ToString(), version).WithExtension(new Extension(".json"));
            file.Parent.CreateDirectory();

            using var queue = new WorkQueue();
            var gameLocation = _game.MetaData().GameLocation();
            
            Utils.Log($"Hashing files for {_game} {version}");
            
            var indexed = await gameLocation
                .EnumerateFiles()
                .PMap(queue, async f =>
                {
                    var hash = await f.FileHashCachedAsync();
                    return new GameFileSourceDownloader.State
                    {
                        Game = _game, 
                        GameFile = f.RelativeTo(gameLocation), 
                        Hash = hash, 
                        GameVersion = version
                    };

                });

            Utils.Log($"Found and hashed {indexed.Length} files");
            await indexed.ToJsonAsync(file, prettyPrint: true);
            return ExitCode.Ok;
        }
    }
}
