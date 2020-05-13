using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using File = System.IO.File;

namespace Wabbajack.Server.Services
{
    /// <summary>
    /// Maintains a concurrent cache of all the files we've downloaded, indexed by Hash. 
    /// </summary>
    public class ArchiveMaintainer
    {
        private AppSettings _settings;
        private ILogger<ArchiveMaintainer> _logger;
        private ConcurrentDictionary<Hash, AbsolutePath> _archives = new ConcurrentDictionary<Hash, AbsolutePath>();

        public ArchiveMaintainer(ILogger<ArchiveMaintainer> logger, AppSettings settings)
        {
            _settings = settings;
            _logger = logger;
        }

        public void Start()
        {
            foreach (var path in _settings.ArchivePath.EnumerateFiles(false))
            {
                try
                {
                    var hash = Hash.FromHex((string)path.FileNameWithoutExtension);
                    _archives[hash] = path;
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex.ToString());
                }
            }
            _logger.Log(LogLevel.Information, $"Found {_archives.Count} archives");
        }

        public async Task<AbsolutePath> Ingest(AbsolutePath file)
        {
            var hash = await file.FileHashAsync();
            if (HaveArchive(hash))
            {
                file.Delete();
                return _archives[hash];
            }
            
            var newPath = _settings.ArchivePath.Combine(hash.ToHex());
            await file.MoveToAsync(newPath);
            _archives[hash] = newPath;
            return _archives[hash];
        }

        public bool HaveArchive(Hash hash)
        {
            return _archives.ContainsKey(hash);
        }

        public bool TryGetPath(Hash hash, out AbsolutePath path)
        {
            return _archives.TryGetValue(hash, out path);
        }
    }
    
    public static class ArchiveMaintainerExtensions 
    {
        public static void UseArchiveMaintainer(this IApplicationBuilder b)
        {
            var poll = (ArchiveMaintainer)b.ApplicationServices.GetService(typeof(ArchiveMaintainer));
            poll.Start();
        }
    
    }
}
