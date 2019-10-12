using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Validation;

namespace Wabbajack.Downloaders
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
        public abstract void Download(Archive a, string destination);

        /// <summary>
        /// Returns true if this link is still valid
        /// </summary>
        /// <returns></returns>
        public abstract bool Verify();

        public abstract IDownloader GetDownloader();
    }
}
