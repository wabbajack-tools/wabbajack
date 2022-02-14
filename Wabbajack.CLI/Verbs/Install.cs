using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Verbs;

public class Install : IVerb
{
    private readonly ILogger<Install> _logger;
    private readonly DownloadDispatcher _dispatcher;
    private readonly Client _wjClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly DTOSerializer _dtos;
    private readonly GameLocator _gameLocator;

    public Install(ILogger<Install> logger, DownloadDispatcher dispatcher, Client wjClient, IServiceProvider serviceProvider,
        DTOSerializer dtos, GameLocator gameLocator)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _wjClient = wjClient;
        _serviceProvider = serviceProvider;
        _dtos = dtos;
        _gameLocator = gameLocator;
    }
    
    public Command MakeCommand()
    {
        var command = new Command("install");
        command.Add(new Option<string>(new[] {"-m", "-machineUrl"}, "MachineURL to download and install"));
        command.Add(new Option<AbsolutePath>(new[] {"-i", "-inputModlist"}, "Input modlist to install"));
        command.Add(new Option<AbsolutePath>(new[] {"-d", "-downloads"}, "Downloads Folder"));
        command.Add(new Option<AbsolutePath>(new[] {"-o", "-output"}, "Install output Folder"));
        command.Description = "Installs a modlist";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    private async Task<int> Run(CancellationToken token, AbsolutePath inputModlist, string machineUrl, AbsolutePath downloads, AbsolutePath output)
    {
        if (string.IsNullOrWhiteSpace(machineUrl) && inputModlist == default)
        {
            _logger.LogError("Need either a inputModlist or a machineUrl");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(machineUrl))
        {
            
            var list = (await _wjClient.LoadLists())
                .FirstOrDefault(l => l.Links.MachineURL == machineUrl);

            if (list == null)
            {
                _logger.LogError("Cannot find list with machineUrl");
                return 2;
            }

            inputModlist = downloads.Combine($"{machineUrl}_modlist.wabbajack");
            var hash = await _dispatcher.Download(new Archive
            {
                Hash = list.DownloadMetadata!.Hash,
                Size = list.DownloadMetadata.Size,
                Name = list.Title,
                State = _dispatcher.Parse(new Uri(list.Links.Download))!
            }, inputModlist, token);
            if (hash != list.DownloadMetadata.Hash)
                _logger.LogError($"Downloaded hash ({hash}) did not match expected hash ({list.DownloadMetadata.Hash}).");
        }

        _logger.LogInformation("Loading Modlist");
        var modlist = await StandardInstaller.LoadFromFile(_dtos, inputModlist);
        
        var installer = StandardInstaller.Create(_serviceProvider, new InstallerConfiguration
        {
            Game = modlist.GameType,
            Downloads = downloads,
            Install = output,
            ModList = modlist,
            ModlistArchive = inputModlist,
            SystemParameters = new SystemParameters()
            {
                ScreenWidth = 1920,
                ScreenHeight = 1080,
                SystemMemorySize = 16_000_000,
                SystemPageSize = 16_000_000,
                VideoMemorySize = 8_000_000
            },
            GameFolder = _gameLocator.GameLocation(modlist.GameType)
        });
        var result = await installer.Begin(token);
        if (result) 
            _logger.LogInformation("Modlist installed, enjoy!");
        else
            _logger.LogInformation("Modlist install error!");
        
        return 0;
    }
}