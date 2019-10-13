using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Downloaders
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

    }
}
