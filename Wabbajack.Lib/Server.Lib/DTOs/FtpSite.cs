namespace Wabbajack.Server.Lib.DTOs;

public enum StorageSpace
{
    AuthoredFiles,
    Patches,
    Mirrors
}

public class FtpSite
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string Hostname { get; set; }

}