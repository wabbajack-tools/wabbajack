using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.CLI.Verbs
{
    [Verb("force-healing", HelpText = "Forces a given source download to be healed by a given new-er download. The new download must be valid.")]
    public class ForceHealing : AVerb
    {
        [Option('o', "old", Required = true, HelpText = "Old Archive (must have an attached .meta)")]
        public string _old { get; set; } = "";

        public AbsolutePath Old => (AbsolutePath)_old;
        [Option('n', "new", Required = true, HelpText = "New Archive (must have an attached .meta)")]
        public string _new { get; set; } = "";
        public AbsolutePath New => (AbsolutePath)_new;
        
        
        protected override async Task<ExitCode> Run()
        {
            Utils.Log("Loading Meta files");
            var oldState = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(Old.WithExtension(Consts.MetaFileExtension).LoadIniFile());
            var newState = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(New.WithExtension(Consts.MetaFileExtension).LoadIniFile());
            Utils.Log("Hashing archives");
            
            var oldHash = await Old.FileHashCachedAsync();
            var newHash = await New.FileHashCachedAsync();

            if (!oldHash.IsValid)
            {
                Utils.Error("Old Hash is not valid!");
                return ExitCode.Error;
            }

            if (!newHash.IsValid)
            {
                Utils.Error("New Hash is not valid!");
                return ExitCode.Error;
            }
            
            var oldArchive = new Archive(oldState) {Hash = oldHash, Size = Old.Size};
            var newArchive = new Archive(newState) {Hash = newHash, Size = New.Size};

            Utils.Log($"Contacting Server to request patch ({oldHash} -> {newHash}");
            Utils.Log($"Response: {await ClientAPI.GetModUpgrade(oldArchive, newArchive, useAuthor: true)}");

            return ExitCode.Ok;
        }
    }
}
