using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.CLI.Console;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
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
    private readonly GameLocator _gameLocator;
    private readonly IConsoleRenderer _console;
    private readonly StatusUpdateBridge _statusBridge;

    public Install(ILogger<Install> logger, Client wjClient, DownloadDispatcher dispatcher, DTOSerializer dtos,
        FileHashCache cache, GameLocator gameLocator, IServiceProvider serviceProvider,
        IConsoleRenderer console, StatusUpdateBridge statusBridge)
    {
        _logger = logger;
        _wjClient = wjClient;
        _dispatcher = dispatcher;
        _dtos = dtos;
        _serviceProvider = serviceProvider;
        _cache = cache;
        _gameLocator = gameLocator;
        _console = console;
        _statusBridge = statusBridge;
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

        _console.Info($"Loading modlist from {wabbajack.FileName}...");
        var modlist = await StandardInstaller.LoadFromFile(_dtos, wabbajack);

        _console.Info($"Installing [bold]{modlist.Name}[/] v{modlist.Version}");
        _console.Info($"Game: {modlist.GameType}");
        _console.Info($"Output: {output}");
        _console.Info($"Downloads: {downloads}");

        var installer = StandardInstaller.Create(_serviceProvider, new InstallerConfiguration
        {
            Downloads = downloads,
            Install = output,
            ModList = modlist,
            Game = modlist.GameType,
            ModlistArchive = wabbajack,
            GameFolder = _gameLocator.GameLocation(modlist.GameType)
        });

        var result = await _statusBridge.WithProgressAsync(
            $"Installing {modlist.Name}",
            async onStatusUpdate =>
            {
                installer.OnStatusUpdate = onStatusUpdate;
                return await installer.Begin(token);
            },
            token);

        if (result == InstallResult.Succeeded)
        {
            _console.Success($"Installation complete: {modlist.Name}");
            return 0;
        }

        _console.Error($"Installation failed: {result}");
        return 2;
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