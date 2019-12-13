using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib
{
    public class ConfirmUpdateOfExistingInstall : ConfirmationIntervention
    {
        public string OutputFolder { get; set; }
        public string ModListName { get; set; }

        public override string ShortDescription { get; } = "Do you want to overwrite existing files?";

        public override string ExtendedDescription
        {
            get =>
                $@"There appears to be a modlist already installed in the output folder. If you continue with the install, 
Any files that exist in {OutputFolder} will be changed to match the files found in the {ModListName} modlist. This means that save games will be removed, custom settings
will be reverted. Are you sure you wish to continue?";
        }
    }
}
