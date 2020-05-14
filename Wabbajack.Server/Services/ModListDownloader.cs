using System;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Server.DataLayer;

namespace Wabbajack.Server.Services
{
    public class ModListDownloader
    {
        private ILogger<ModListDownloader> _logger;
        private AppSettings _settings;
        private ArchiveMaintainer _maintainer;
        private SqlService _sql;

        public ModListDownloader(ILogger<ModListDownloader> logger, AppSettings settings, ArchiveMaintainer maintainer, SqlService sql)
        {
            _logger = logger;
            _settings = settings;
            _maintainer = maintainer;
            _sql = sql;
        }

        public void Start()
        {
            if (_settings.RunBackEndJobs)
            {
                Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            _logger.Log(LogLevel.Information, "Checking for updated mod lists");
                            await CheckForNewLists();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error checking list");
                        }

                        await Task.Delay(TimeSpan.FromMinutes(5));

                    }
                });
            }
        }

        public async Task<int> CheckForNewLists()
        {
            int downloaded = 0;
            var lists = await ModlistMetadata.LoadFromGithub();
            foreach (var list in lists)
            {
                try
                {
                    if (await _sql.HaveIndexedModlist(list.Links.MachineURL, list.DownloadMetadata.Hash))
                        continue;


                    if (!_maintainer.HaveArchive(list.DownloadMetadata!.Hash))
                    {
                        _logger.Log(LogLevel.Information, $"Downloading {list.Links.MachineURL}");
                        var tf = new TempFile();
                        var state = DownloadDispatcher.ResolveArchive(list.Links.Download);
                        if (state == null)
                        {
                            _logger.Log(LogLevel.Error,
                                $"Now downloader found for list {list.Links.MachineURL} : {list.Links.Download}");
                            continue;
                        }

                        downloaded += 1;
                        await state.Download(new Archive(state) {Name = $"{list.Links.MachineURL}.wabbajack"}, tf.Path);
                        var hash = await tf.Path.FileHashAsync();
                        if (hash != list.DownloadMetadata.Hash)
                        {
                            _logger.Log(LogLevel.Error,
                                $"Downloaded modlist {list.Links.MachineURL} {list.DownloadMetadata.Hash} didn't match metadata hash of {hash}");
                            await _sql.IngestModList(list.DownloadMetadata.Hash, list, new ModList(), true);
                            continue;
                        }

                        await _maintainer.Ingest(tf.Path);
                    }

                    _maintainer.TryGetPath(list.DownloadMetadata.Hash, out var modlistPath);
                    ModList modlist;
                    await using (var fs = modlistPath.OpenRead())
                    using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                    await using (var entry = zip.GetEntry("modlist")?.Open())
                    {
                        if (entry == null)
                        {
                            Utils.Log($"Bad Modlist {list.Links.MachineURL}");
                            continue;
                        }

                        try
                        {
                            modlist = entry.FromJson<ModList>();
                        }
                        catch (JsonReaderException ex)
                        {
                            Utils.Log($"Bad JSON format for {list.Links.MachineURL}");
                            continue;
                        }
                    }

                    await _sql.IngestModList(list.DownloadMetadata!.Hash, list, modlist, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error downloading modlist {list.Links.MachineURL}");
                }
            }
            _logger.Log(LogLevel.Information, $"Done checking modlists. Downloaded {downloaded} new lists");

            var fc = await _sql.EnqueueModListFilesForIndexing();
            _logger.Log(LogLevel.Information, $"Enqueing {fc} files for downloading");
            
            return downloaded;
        }
    }
    
    public static class ModListDownloaderExtensions 
    {
        public static void UseModListDownloader(this IApplicationBuilder b)
        {
            var poll = (ModListDownloader)b.ApplicationServices.GetService(typeof(ModListDownloader));
            poll.Start();
        }
    
    }
}
