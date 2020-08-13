using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack.CLI.Verbs
{
    [Verb("nexus-permissions", HelpText = "Get the nexus permissions for a mod")]
    public class NexusPermissions : AVerb
    {
        [Option('m', "mod-id", Required = true, HelpText = "Mod Id")]
        public long ModId { get; set; } = 0;
        
        [Option('g', "game", Required = true, HelpText = "Game Name")]
        public string GameName { get; set; } = "";
        protected override async Task<ExitCode> Run()
        {
            var game = GameRegistry.GetByFuzzyName(GameName).Game;
            var p = await HTMLInterface.GetUploadPermissions(game, ModId);
            Console.WriteLine($"Game: {game}");
            Console.WriteLine($"ModId: {ModId}");
            Console.WriteLine($"Permissions: {p}");
            return ExitCode.Ok;
        }
    }
}
