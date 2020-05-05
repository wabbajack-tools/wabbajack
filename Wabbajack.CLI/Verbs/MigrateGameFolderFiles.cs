using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib.Tasks;

namespace Wabbajack.CLI.Verbs
{
    [Verb("migrate-game-folder", HelpText = "Migrates game files into the 'Game Folder Files' in a MO2 directory")]
    public class MigrateGameFolderFiles : AVerb
    {
        [IsDirectory(CustomMessage = "Downloads folder at %1 does not exist!")]
        [Option('i', "input", HelpText = "Input Mod Organizer 2 Folder", Required = true)]
        public string MO2Folder { get; set; } = "";


        protected override async Task<ExitCode> Run()
        {
            if (await MigrateGameFolder.Execute((AbsolutePath)MO2Folder))
            {
                return ExitCode.Ok;
            };
            return ExitCode.Error;
        }
    }
}
