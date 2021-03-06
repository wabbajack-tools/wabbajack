﻿using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Validation;

namespace Wabbajack.CLI.Verbs
{
    [Verb("validate", HelpText = @"Validates a Modlist")]
    public class Validate : AVerb
    {
        [IsFile(CustomMessage = "The modlist file %1 does not exist!", Extension = Consts.ModListExtensionString)]
        [Option('i', "input", Required = true, HelpText = @"Modlist file")]
        public string Input { get; set; } = "";

        /// <summary>
        /// Runs the Validation of a Modlist
        /// </summary>
        /// <returns></returns>
        protected override async Task<ExitCode> Run()
        {
            ModList modlist;

            try
            {
                modlist = AInstaller.LoadFromFile((AbsolutePath)Input);
            }
            catch (Exception e)
            {
                return CLIUtils.Exit($"Error while loading the Modlist!\n{e}", ExitCode.Error);
            }

            if (modlist == null)
            {
                return CLIUtils.Exit($"The Modlist could not be loaded!", ExitCode.Error);
            }
                

            var queue = new WorkQueue();

            try
            {
                ValidateModlist.RunValidation(modlist).RunSynchronously();
            }
            catch (Exception e)
            {
                return CLIUtils.Exit($"Error during Validation!\n{e}", ExitCode.Error);
            }

            return CLIUtils.Exit("The Modlist passed the Validation", 0);
        }
    }
}
