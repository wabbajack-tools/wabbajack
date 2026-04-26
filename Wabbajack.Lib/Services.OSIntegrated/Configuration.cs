using Wabbajack.Paths;

namespace Wabbajack.Services.OSIntegrated;

public class Configuration
{
    public AbsolutePath ModListsDownloadLocation { get; set; }
    public AbsolutePath SavedSettingsLocation { get; set; }
    public AbsolutePath EncryptedDataLocation { get; set; }
    public AbsolutePath LogLocation { get; set; }
    
    public AbsolutePath ImageCacheLocation { get; set; }
}