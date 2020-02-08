using System;
using System.Reactive.Linq;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Validation;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.CLI.Verbs
{
    [Verb("validate", HelpText = @"Validates a Modlist")]
    public class Validate
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
        public static int Run(Validate opts)
        {
            if (!File.Exists(opts.Input))
                return -1;

            if (!opts.Input.EndsWith(Common.Consts.ModListExtension))
                return -1;

            ModList modlist;

            try
            {
                modlist = AInstaller.LoadFromFile(opts.Input);
            }
            catch (Exception)
            {
                return 1;
            }

            if (modlist == null)
                return 1;

            var queue = new WorkQueue();

            try
            {
                ValidateModlist.RunValidation(queue, modlist).RunSynchronously();
            }
            catch (Exception)
            {
                return 1;
            }

            return 0;
        }
    }
}
