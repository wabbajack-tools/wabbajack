using Microsoft.Extensions.Configuration;

namespace Wabbajack.BuildServer
{
    public class AppSettings
    {
        public AppSettings(IConfiguration config)
        {
            config.Bind("WabbajackSettings", this);
        }
        
        public string DownloadDir { get; set; }
        public string ArchiveDir { get; set; }
        
        public bool MinimalMode { get; set; }
        
        public bool RunFrontEndJobs { get; set; }
        public bool RunBackEndJobs { get; set; }
    }
}
