namespace Wabbajack.DTOs;

public class ApplicationInfo
{
    public string ApplicationName { get; set; }
    public string ApplicationSlug { get; set; }
    public string Version { get; set; }
    public string OperatingSystemDescription { get; set; }
    public string Platform { get; set; }
    public string ApplicationSha { get; set; }
    public string RuntimeIdentifier { get; set; }
    public string OSVersion { get; set; }
    public string UserAgent => $"{ApplicationSlug}/{Version} ({OSVersion}; {Platform})";
}