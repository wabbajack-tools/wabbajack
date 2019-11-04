using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.Downloaders
{
    public static class DownloadDispatcher
    {
        public static readonly List<IDownloader> Downloaders = new List<IDownloader>()
        {
            new MegaDownloader(),
            new DropboxDownloader(),
            new GoogleDriveDownloader(),
            new ModDBDownloader(),
            new NexusDownloader(),
            new MediaFireDownloader(),
            new ManualDownloader(),
            new HTTPDownloader()
        };

        private static readonly Dictionary<Type, IDownloader> IndexedDownloaders;

        static DownloadDispatcher()
        {
            IndexedDownloaders = Downloaders.ToDictionary(d => d.GetType());
        }

        public static T GetInstance<T>()
        {
            return (T)IndexedDownloaders[typeof(T)];
        }

        public static AbstractDownloadState ResolveArchive(dynamic ini)
        {
            return Downloaders.Select(d => d.GetDownloaderState(ini)).FirstOrDefault(result => result != null);
        }

        /// <summary>
        /// Reduced version of Resolve archive that requires less information, but only works
        /// with a single URL string
        /// </summary>
        /// <param name="ini"></param>
        /// <returns></returns>
        public static AbstractDownloadState ResolveArchive(string url)
        {
            return Downloaders.OfType<IUrlDownloader>().Select(d => d.GetDownloaderState(url)).FirstOrDefault(result => result != null);
        }

        public static void PrepareAll(IEnumerable<AbstractDownloadState> states)
        {
            states.Select(s => s.GetDownloader().GetType())
                  .Distinct()
                  .Do(t => Downloaders.First(d => d.GetType() == t).Prepare());
        }
    }
}
