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
        
        public bool JobScheduler { get; set; }
        public bool JobRunner { get; set; }
        
        public bool RunFrontEndJobs { get; set; }
        public bool RunBackEndJobs { get; set; }
        
        public string BunnyCDN_User { get; set; }
        public string BunnyCDN_Password { get; set; }
        
        public string SqlConnection { get; set; }
    }
}
