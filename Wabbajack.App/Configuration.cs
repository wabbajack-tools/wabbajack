using Wabbajack.Paths;

namespace Wabbajack.App;

public class Configuration
{
    public AbsolutePath ModListsDownloadLocation { get; set; }
    public AbsolutePath SavedSettingsLocation { get; set; }
    public AbsolutePath EncryptedDataLocation { get; set; }
    public AbsolutePath LogLocation { get; set; }
    
    public AbsolutePath ImageCacheLocation { get; set; }
}