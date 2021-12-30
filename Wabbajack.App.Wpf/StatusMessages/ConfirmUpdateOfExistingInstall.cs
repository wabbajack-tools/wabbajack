using Wabbajack.Paths;

namespace Wabbajack
{
    public class ConfirmUpdateOfExistingInstall : ConfirmationIntervention
    {
        public AbsolutePath OutputFolder { get; set; }
        public string ModListName { get; set; } = string.Empty;

        public override string ShortDescription { get; } = "Do you want to update existing files?";

        public override string ExtendedDescription
        {
            get =>
                $@"There appears to be a modlist already installed in the output folder. If you continue with the install, 
Any files that exist in {OutputFolder} will be changed to match the files found in the {ModListName} modlist. Custom settings
will be reverted, but saved games will not be deleted. Are you sure you wish to continue?";
        }
    }
}
