using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Validation;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.CLI.Verbs
{
    [Verb("validate", HelpText = @"Validates a Modlist")]
    public class Validate : AVerb
    {
        [IsFile(CustomMessage = "The modlist file %1 does not exist!")]
        [Option('i', "input", Required = true, HelpText = @"Modlist file")]
        public string? Input { get; set; }

        /// <summary>
        /// Runs the Validation of a Modlist
        /// </summary>
        /// <returns>
        /// <para>
        /// <c>-1</c> bad Input
        /// <c>0</c> valid modlist
        /// <c>1</c> broken modlist
        /// </para>
        /// </returns>
        protected override async Task<int> Run()
        {
            if (Input != null && !Input.EndsWith(Consts.ModListExtension))
                return CLIUtils.Exit($"The file {Input} does not end with {Consts.ModListExtension}!", -1);

            ModList modlist;

            try
            {
                modlist = AInstaller.LoadFromFile(Input);
            }
            catch (Exception e)
            {
                return CLIUtils.Exit($"Error while loading the Modlist!\n{e}", 1);
            }

            if (modlist == null)
            {
                return CLIUtils.Exit($"The Modlist could not be loaded!", 1);
            }
                

            var queue = new WorkQueue();

            try
            {
                ValidateModlist.RunValidation(queue, modlist).RunSynchronously();
            }
            catch (Exception e)
            {
                return CLIUtils.Exit($"Error during Validation!\n{e}", 1);
            }

            return CLIUtils.Exit("The Modlist passed the Validation", 0);
        }
    }
}
