using Microsoft.Extensions.Configuration;
using Wabbajack.Common;

namespace Wabbajack.BuildServer
{
    public class AppSettings
    {
        public AppSettings(IConfiguration config)
        {
            config.Bind("WabbajackSettings", this);
        }
        
        public string DownloadDir { get; set; }
        public AbsolutePath DownloadPath => (AbsolutePath)DownloadDir;
        public string ArchiveDir { get; set; }
        public AbsolutePath ArchivePath => (AbsolutePath)ArchiveDir;
        
        public string TempFolder { get; set; }

        public AbsolutePath TempPath => (AbsolutePath)TempFolder;
        
        public bool JobScheduler { get; set; }
        public bool JobRunner { get; set; }
        
        public bool RunFrontEndJobs { get; set; }
        public bool RunBackEndJobs { get; set; }
        
        public string BunnyCDN_User { get; set; }
        public string BunnyCDN_Password { get; set; }
        public string SqlConnection { get; set; }

        public int MaxJobs { get; set; } = 2;
    }
}
