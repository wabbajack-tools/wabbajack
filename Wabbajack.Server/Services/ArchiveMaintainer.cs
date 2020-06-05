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

        public ArchiveMaintainer(ILogger<ArchiveMaintainer> logger, AppSettings settings)
        {
            _settings = settings;
            _logger = logger;
            _logger.Log(LogLevel.Information, "Creating Archive Maintainer");
        }

        public void Start()
        {
            _logger.Log(LogLevel.Information, $"Found {_settings.ArchivePath.EnumerateFiles(false).Count()} archives");
        }

        private AbsolutePath ArchivePath(Hash hash)
        {
            return _settings.ArchivePath.Combine(hash.ToHex());
        }

        public async Task<AbsolutePath> Ingest(AbsolutePath file)
        {
            var hash = await file.FileHashAsync();
            var path = ArchivePath(hash);
            if (HaveArchive(hash))
            {
                await file.DeleteAsync();
                return path;
            }
            
            var newPath = _settings.ArchivePath.Combine(hash.ToHex());
            await file.MoveToAsync(newPath);
            return path;
        }

        public bool HaveArchive(Hash hash)
        {
            return ArchivePath(hash).Exists;
        }

        public bool TryGetPath(Hash hash, out AbsolutePath path)
        {
            path = ArchivePath(hash);
            return path.Exists;
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
