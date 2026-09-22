using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.VFS;

namespace Wabbajack.CLI.Verbs;

public class Install
{
    private readonly ILogger<Install> _logger;
    private readonly Client _wjClient;
    private readonly DownloadDispatcher _dispatcher;
    private readonly IServiceProvider _serviceProvider;
    private readonly DTOSerializer _dtos;
    private readonly FileHashCache _cache;

    public Install(ILogger<Install> logger, Client wjClient, DownloadDispatcher dispatcher, DTOSerializer dtos, 
        FileHashCache cache, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _wjClient = wjClient;
        _dispatcher = dispatcher;
        _dtos = dtos;
        _serviceProvider = serviceProvider;
        _cache = cache;
    }

    public static VerbDefinition Definition = new VerbDefinition("install", "Installs a wabbajack file", new[]
    {
        new OptionDefinition(typeof(AbsolutePath), "w", "wabbajack", "Wabbajack file"),
        new OptionDefinition(typeof(string), "m", "machineUrl", "Machine url to download"),
        new OptionDefinition(typeof(AbsolutePath), "o", "output", "Output path"),
        new OptionDefinition(typeof(AbsolutePath), "d", "downloads", "Downloads path")
    });

    internal async Task<int> Run(AbsolutePath wabbajack, AbsolutePath output, AbsolutePath downloads, string machineUrl, CancellationToken token)
    {
        if (!string.IsNullOrEmpty(machineUrl))
        {
            if (!await DownloadMachineUrl(machineUrl, wabbajack, token))
                return 1;
        }

        var modlist = await StandardInstaller.LoadFromFile(_dtos, wabbajack);

        // GameFolder is left unset: the game-installed preflight check locates it and reports clearly
        // when it cannot, rather than the locator throwing here.
        return await PreflightInstall.Run(_serviceProvider, new InstallerConfiguration
        {
            Downloads = downloads,
            Install = output,
            ModList = modlist,
            Game = modlist.GameType,
            OtherGames = modlist.OtherGames,
            ModlistArchive = wabbajack,
            GameFolder = default
        }, _logger, token);
    }

    private async Task<bool> DownloadMachineUrl(string machineUrl, AbsolutePath wabbajack, CancellationToken token)
    {
        _logger.LogInformation("Downloading {MachineUrl}", machineUrl);

        var lists = await _wjClient.LoadLists();
        var list = lists.FirstOrDefault(l => l.NamespacedName == machineUrl);
        if (list == null)
        {
            _logger.LogInformation("Couldn't find list {MachineUrl}", machineUrl);
            return false;
        }
        
        if (wabbajack.FileExists() && await _cache.FileHashCachedAsync(wabbajack, token) == list.DownloadMetadata!.Hash)
        {
            _logger.LogInformation("File already exists, using cached file");
            return true;
        }

        var state = _dispatcher.Parse(new Uri(list.Links.Download));

        await _dispatcher.Download(new Archive
        {
            Name = wabbajack.FileName.ToString(),
            Hash = list.DownloadMetadata!.Hash,
            Size = list.DownloadMetadata.Size,
            State = state!
        }, wabbajack, token);

        return true;
    }
}