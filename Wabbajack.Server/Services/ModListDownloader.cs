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
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.Services
{
    public class ModListDownloader
    {
        private ILogger<ModListDownloader> _logger;
        private AppSettings _settings;
        private ArchiveMaintainer _maintainer;
        private SqlService _sql;
        private DiscordWebHook _discord;

        public ModListDownloader(ILogger<ModListDownloader> logger, AppSettings settings, ArchiveMaintainer maintainer, SqlService sql, DiscordWebHook discord)
        {
            _logger = logger;
            _settings = settings;
            _maintainer = maintainer;
            _sql = sql;
            _discord = discord;
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
                        await _discord.Send(Channel.Ham,
                            new DiscordMessage {Content = $"Downloading {list.Links.MachineURL} - {list.DownloadMetadata.Hash}"});
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
                    await using (var fs = await modlistPath.OpenRead())
                    using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                    await using (var entry = zip.GetEntry("modlist")?.Open())
                    {
                        if (entry == null)
                        {
                            _logger.LogWarning($"Bad Modlist {list.Links.MachineURL}");
                            await _discord.Send(Channel.Ham,
                                new DiscordMessage {Content = $"Bad Modlist  {list.Links.MachineURL} - {list.DownloadMetadata.Hash}"});
                            continue;
                        }

                        try
                        {
                            modlist = entry.FromJson<ModList>();
                        }
                        catch (JsonReaderException ex)
                        {
                            _logger.LogWarning($"Bad Modlist {list.Links.MachineURL}");
                            await _discord.Send(Channel.Ham,
                                new DiscordMessage {Content = $"Bad Modlist  {list.Links.MachineURL} - {list.DownloadMetadata.Hash}"});
                            continue;
                        }
                    }

                    await _sql.IngestModList(list.DownloadMetadata!.Hash, list, modlist, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error downloading modlist {list.Links.MachineURL}");
                    await _discord.Send(Channel.Ham,
                        new DiscordMessage {Content = $"Error downloading modlist {list.Links.MachineURL} - {list.DownloadMetadata.Hash}"});
                }
            }
            _logger.Log(LogLevel.Information, $"Done checking modlists. Downloaded {downloaded} new lists");
            if (downloaded > 0) 
                await _discord.Send(Channel.Ham,
                    new DiscordMessage {Content = $"Downloaded {downloaded} new lists"});

            var fc = await _sql.EnqueueModListFilesForIndexing();
            _logger.Log(LogLevel.Information, $"Enqueing {fc} files for downloading");
            if (fc > 0) 
                await _discord.Send(Channel.Ham,
                    new DiscordMessage {Content = $"Enqueing {fc} files for downloading"});
            
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
