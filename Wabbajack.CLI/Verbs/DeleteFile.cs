using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Lib.FileUploader;

namespace Wabbajack.CLI.Verbs
{
    [Verb("delete-uploaded-file", HelpText = "Delete a file you uploaded to the CDN. Cannot delete other user's files")]
    public class DeleteFile : AVerb
    {
        [Option('n', "name", Required = true, HelpText = @"Full name (as returned by my-files) of the file")]
        public string? Name { get; set; }

        protected override async Task<int> Run()
        {
            Console.WriteLine(await AuthorAPI.DeleteFile(Name));
            return 0;
        }
    }
}
