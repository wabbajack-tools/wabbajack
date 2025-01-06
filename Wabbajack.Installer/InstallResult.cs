namespace Wabbajack.Installer
{
    public enum InstallResult
    {
        Succeeded,
        Cancelled,
        Errored,
        GameMissing,
        GameInvalid,
        DownloadFailed,
        NotEnoughSpace,
    }
}
