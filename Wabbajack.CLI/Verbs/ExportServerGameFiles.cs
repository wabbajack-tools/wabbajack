using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.CLI.Verbs
{
    [Verb("export-server-game-files", HelpText = "Exports all the game file data from the server to the output folder")]
    public class ExportServerGameFiles : AVerb
    {
        [Option('o', "output", Required = true, HelpText = @"Output folder in which the files will be placed")]
        public string OutputFolder { get; set; } = "";

        private AbsolutePath _outputFolder => (AbsolutePath)OutputFolder;

        protected override async Task<ExitCode> Run()
        {
            var games = await ClientAPI.GetServerGamesAndVersions();
            foreach (var (game, version) in games)
            {
                Utils.Log($"Exporting {game} {version}");
                var file = _outputFolder.Combine(game.ToString(), version).WithExtension(new Extension(".json"));
                file.Parent.CreateDirectory();
                var files = await ClientAPI.GetGameFilesFromServer(game, version);
                await files.ToJsonAsync(file, prettyPrint:true);
            }

            return ExitCode.Ok;
        }
    }
}
