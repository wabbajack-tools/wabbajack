using Wabbajack.Installer;

namespace Wabbajack;

public static class InstallResultHelper
{
    public static string GetTitle(this InstallResult result)
    {
        return result switch
        {
            InstallResult.Succeeded => "Modlist installed",
            InstallResult.Cancelled => "Cancelled",
            InstallResult.Errored => "An error occurred",
            InstallResult.GameMissing => "Game not found",
            InstallResult.GameInvalid => "Game installation invalid",
            InstallResult.DownloadFailed => "Download failed",
            InstallResult.NotEnoughSpace => "Not enough space",
            _ => ""
        };
    }
    public static string GetDescription(this InstallResult result)
    {
        return result switch
        {
            InstallResult.Succeeded => "The modlist installation completed successfully. Start up Mod Organizer in the installation directory, hit run on the top right and enjoy playing!",
            InstallResult.Cancelled => "The modlist installation was cancelled.",
            InstallResult.Errored => "The modlist installation has failed because of an unknown error. Check the log for more information.",
            InstallResult.GameMissing => "The modlist installation has failed because the game could not be found. Please make sure a valid copy of the game is installed.",
            InstallResult.GameInvalid => "The modlist installation has failed because not all required game files could be found. Verify all game files are present and retry installation.",
            InstallResult.DownloadFailed => "The modlist installation has failed because one or more required files could not be sourced. Try manually sourcing these files below.",
            InstallResult.NotEnoughSpace => "The modlist installation has failed because not enough free space was available on the disk. Please free up enough space and retry the installation.",
            _ => ""
        };
    }
}
