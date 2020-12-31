using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.FileUploader;

namespace Wabbajack.CLI.Verbs
{
    [Verb("all-known-download-states", HelpText = "Print known Ini info for a given hash")]
    public class AllKnownDownloadStates : AVerb
    {
        [Option('i', "input", Required = true, HelpText = "Input Hash")]
        public string _input { get; set; } = "";

        public Hash Input => Hash.Interpret(_input);
        protected override async Task<ExitCode> Run()
        {
            var states = await ClientAPI.InferAllDownloadStates(Input);
            Console.WriteLine($"Found {states.Length} states");

            foreach (var archive in states)
            {
                Console.WriteLine("----");
                Console.WriteLine($"Name : {archive.State.PrimaryKeyString}");
                Console.WriteLine($"Is Valid: {await archive.State.Verify(archive)}");
                Console.WriteLine("------ Begin INI--------");
                Console.WriteLine(archive.State.GetMetaIniString());
                Console.WriteLine("------ End INI  --------");
            }

            return ExitCode.Ok;
        }
    }
}
