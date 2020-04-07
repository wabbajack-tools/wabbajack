using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Lib.FileUploader;

namespace Wabbajack.CLI.Verbs
{
    [Verb("server-log", HelpText = @"Get the latest server log entries", Hidden = false)]
    public class ServerLog : AVerb
    {
        protected override async Task<ExitCode> Run()
        {
            Console.WriteLine(await AuthorAPI.GetServerLog());
            return 0;
        }
    }
}
