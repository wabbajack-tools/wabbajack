using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Validation;
using Wabbajack.VirtualFileSystem;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.CLI.Verbs
{
    [Verb("validate", HelpText = @"Validates a Modlist")]
    public class Validate : AVerb
    {
        [Option('i', "input", Required = true, HelpText = @"Modlist file")]
        public string Input { get; set; }

        /// <summary>
        /// Runs the Validation of a Modlist
        /// </summary>
        /// <param name="opts"></param>
        /// <returns>
        /// <para>
        /// <c>-1</c> bad Input
        /// <c>0</c> valid modlist
        /// <c>1</c> broken modlist
        /// </para>
        /// </returns>
        protected override async Task<int> Run()
        {
            if (!File.Exists(Input))
            {
                CLIUtils.Log($"The file {Input} does not exist!");
                return -1;
            }


            if (!Input.EndsWith(Consts.ModListExtension))
            {
                CLIUtils.Log($"The file {Input} does not end with {Consts.ModListExtension}!");
                return -1;
            }
            
            ModList modlist;

            try
            {
                modlist = AInstaller.LoadFromFile(Input);
            }
            catch (Exception e)
            {
                CLIUtils.Log($"Error while loading the Modlist!\n{e}");
                return 1;
            }

            if (modlist == null)
            {
                CLIUtils.Log($"The Modlist could not be loaded!");
                return 1;
            }
                

            var queue = new WorkQueue();

            try
            {
                ValidateModlist.RunValidation(queue, modlist).RunSynchronously();
            }
            catch (Exception e)
            {
                CLIUtils.Log($"Error during Validation!\n{e}");
                return 1;
            }

            CLIUtils.Log("The Modlist passed the Validation");
            return 0;
        }
    }
}
