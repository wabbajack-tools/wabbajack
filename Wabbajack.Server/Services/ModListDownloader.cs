using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths.IO;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.Services
{
    public class ModListDownloader : AbstractService<ModListDownloader, int>
    {
        private ArchiveMaintainer _maintainer;
        private SqlService _sql;
        private DiscordWebHook _discord;
        private readonly Client _wjClient;
        private readonly TemporaryFileManager _manager;
        private readonly DownloadDispatcher _dispatcher;
        private readonly DTOSerializer _dtos;

        public ModListDownloader(ILogger<ModListDownloader> logger, AppSettings settings, ArchiveMaintainer maintainer, 
            SqlService sql, DiscordWebHook discord, QuickSync quickSync, Client wjClient, TemporaryFileManager manager,
            DownloadDispatcher dispatcher, DTOSerializer dtos)
        : base(logger, settings, quickSync, TimeSpan.FromMinutes(1))
        {
            _logger = logger;
            _settings = settings;
            _maintainer = maintainer;
            _sql = sql;
            _discord = discord;
            _wjClient = wjClient;
            _manager = manager;
            _dispatcher = dispatcher;
            _dtos = dtos;
        }


        public override async Task<int> Execute()
        {
            int downloaded = 0;
            var lists = await _wjClient.LoadLists();
            
            foreach (var list in lists)
            {
                try
                {
                    ReportStarting(list.Links.MachineURL);
                    if (await _sql.HaveIndexedModlist(list.Links.MachineURL, list.DownloadMetadata.Hash))
                        continue;


                    if (!_maintainer.HaveArchive(list.DownloadMetadata!.Hash))
                    {
                        _logger.Log(LogLevel.Information, $"Downloading {list.Links.MachineURL}");
                        await _discord.Send(Channel.Ham,
                            new DiscordMessage
                            {
                                Content = $"Downloading {list.Links.MachineURL} - {list.DownloadMetadata.Hash}"
                            });
                        var tf = _manager.CreateFile();
                        var state = _dispatcher.Parse(new Uri(list.Links.Download));
                        if (state == null)
                        {
                            _logger.Log(LogLevel.Error,
                                $"Now downloader found for list {list.Links.MachineURL} : {list.Links.Download}");
                            continue;
                        }

                        downloaded += 1;
                        await _dispatcher.Download(new Archive{State = state, Name = $"{list.Links.MachineURL}.wabbajack"}, tf.Path, CancellationToken.None);
                        var hash = await tf.Path.Hash();
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

                    modlist = await StandardInstaller.LoadFromFile(_dtos, modlistPath);

                    await _discord.Send(Channel.Ham,
                        new DiscordMessage
                        {
                            Content = $"Ingesting {list.Links.MachineURL} version {modlist.Version}"
                        });
                    await _sql.IngestModList(list.DownloadMetadata!.Hash, list, modlist, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error downloading modlist {list.Links.MachineURL}");
                    await _discord.Send(Channel.Ham,
                        new DiscordMessage
                        {
                            Content =
                                $"Error downloading modlist {list.Links.MachineURL} - {list.DownloadMetadata.Hash} - {ex.Message}"
                        });
                }
                finally
                {
                    ReportEnding(list.Links.MachineURL);
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
