using System;
using System.Threading.Tasks;
using CommandLine;
using Compression.BSA;
using Wabbajack.Common;

namespace Wabbajack.CLI.Verbs
{
    [Verb("extract-bsa", HelpText = "Extracts a BSA/BA2 into a folder")]
    public class ExtractBSA : AVerb
    {
        [Option('o', "output", Required = true, HelpText = @"Output folder to extract to")]
        public string OutputFolder { get; set; } = "";
        
        [IsFile(CustomMessage = "The input file %1 does not exist!")]
        [Option('i', "input", Required = true, HelpText = @"BSA/BA2 to extract")]
        public string InputFile { get; set; } = "";
        
        protected override async Task<ExitCode> Run()
        {
            Console.WriteLine($"Extracting {InputFile} to {OutputFolder}");
            var bsa = await BSADispatch.OpenRead((AbsolutePath)InputFile);
            foreach (var file in bsa.Files)
            {
                Console.WriteLine($"Extracting {file.Path}");
                var ofile = file.Path.RelativeTo((AbsolutePath)OutputFolder);
                ofile.Parent.CreateDirectory();
                await using var ostream = await ofile.Create();
                await file.CopyDataTo(ostream);
            }

            return ExitCode.Ok;
        }
    }
}
