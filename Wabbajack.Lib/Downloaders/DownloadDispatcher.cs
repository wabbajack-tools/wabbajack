using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.Downloaders
{
    public static class DownloadDispatcher
    {
        public static readonly List<IDownloader> Downloaders = new List<IDownloader>()
        {
            new GameFileSourceDownloader(),
            new MegaDownloader(),
            new DropboxDownloader(),
            new GoogleDriveDownloader(),
            new ModDBDownloader(),
            new NexusDownloader(),
            new MediaFireDownloader(),
            new LoversLabDownloader(),
            new VectorPlexusDownloader(),
            new DeadlyStreamDownloader(),
            new AFKModsDownloader(),
            new TESAllianceDownloader(),
            new HTTPDownloader(),
            new ManualDownloader(),
        };

        private static readonly Dictionary<Type, IDownloader> IndexedDownloaders;

        static DownloadDispatcher()
        {
            IndexedDownloaders = Downloaders.ToDictionary(d => d.GetType());
        }

        public static T GetInstance<T>() where T : IDownloader
        {
            var inst = (T)IndexedDownloaders[typeof(T)];
            inst.Prepare();
            return inst;
        }

        public static async Task<AbstractDownloadState> ResolveArchive(dynamic ini)
        {
            var states = await Task.WhenAll(Downloaders.Select(d => (Task<AbstractDownloadState>)d.GetDownloaderState(ini)));
            return states.FirstOrDefault(result => result != null);
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
