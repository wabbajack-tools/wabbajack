using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.LibCefHelpers;

namespace Wabbajack.CLI.Verbs
{
    [Verb("download-from-meta", HelpText = "Download a file from a given .meta file")]
    public class DownloadFromMeta : AVerb
    {
        [Option('m', "meta", Required = true, HelpText = "Meta to read from which to source the download")]
        public string Meta { get; set; } = "";

        [Option('o', "output", Required = true, HelpText = "Output file name")]
        public string Output { get; set; } = "";


        protected override async Task<ExitCode> Run()
        {
            var state = await DownloadDispatcher.ResolveArchive(((AbsolutePath)Meta).LoadIniFile(), true);
            if (state == null)
            {
                Console.WriteLine("Cannot find downloader for input meta");
                return ExitCode.Error;
            }

            var astate = (AbstractDownloadState)state;

            Console.WriteLine($"Downloading {astate.PrimaryKeyString}");
            await astate.Download(new Archive(astate), (AbsolutePath)Output);
            
            return ExitCode.Ok;
        }
    }
}
