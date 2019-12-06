using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib.Downloaders
{
    /// <summary>
    /// Base for all abstract downloaders
    /// </summary>
    public abstract class AbstractDownloadState
    {
        /// <summary>
        /// Returns true if this file is allowed to be downloaded via whitelist
        /// </summary>
        /// <param name="whitelist"></param>
        /// <returns></returns>
        public abstract bool IsWhitelisted(ServerWhitelist whitelist);

        /// <summary>
        /// Downloads this file to the given destination location
        /// </summary>
        /// <param name="destination"></param>
        public abstract Task Download(Archive a, string destination);

        public async Task Download(string destination)
        {
            await Download(new Archive {Name = Path.GetFileName(destination)}, destination);
        }

        /// <summary>
        /// Returns true if this link is still valid
        /// </summary>
        /// <returns></returns>
        public abstract Task<bool> Verify();

        public abstract IDownloader GetDownloader();

        public abstract string GetReportEntry(Archive a);
    }
}
