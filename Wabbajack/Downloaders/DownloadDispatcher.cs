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
        private static List<IDownloader> _downloaders = new List<IDownloader>()
        {
            new MegaDownloader(),
            new DropboxDownloader(),
            new GoogleDriveDownloader(),
            new HTTPDownloader(),
            new NexusDownloader()
        };

        private static Dictionary<Type, IDownloader> _indexedDownloaders;

        static DownloadDispatcher()
        {
            _indexedDownloaders = _downloaders.ToDictionary(d => d.GetType());
        }

        public static T GetInstance<T>()
        {
            return (T)_indexedDownloaders[typeof(T)];
        }

        public static AbstractDownloadState ResolveArchive(dynamic ini)
        {
            foreach (var d in _downloaders)
            {
                var result = d.GetDownloaderState(ini);
                if (result != null) return result;
            }

            return null;
        }

    }
}
