using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Compiler;
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

public class InstallCompileInstallVerify : IVerb
{
    private readonly ILogger<InstallCompileInstallVerify> _logger;
    private readonly Client _wjClient;
    private readonly DownloadDispatcher _dispatcher;

    private readonly DTOSerializer _dtos;
    private readonly IServiceProvider _serviceProvider;
    private readonly FileHashCache _cache;
    private readonly GameLocator _gameLocator;
    private readonly CompilerSettingsInferencer _inferencer;

    public InstallCompileInstallVerify(ILogger<InstallCompileInstallVerify> logger, Client wjClient, DownloadDispatcher dispatcher, DTOSerializer dtos, 
        FileHashCache cache, GameLocator gameLocator, IServiceProvider serviceProvider, CompilerSettingsInferencer inferencer)
    {
        _logger = logger;
        _wjClient = wjClient;
        _dispatcher = dispatcher;
        _dtos = dtos;
        _serviceProvider = serviceProvider;
        _cache = cache;
        _gameLocator = gameLocator;
        _inferencer = inferencer;
    }
    
    public Command MakeCommand()
    {
        var command = new Command("install-compile-install-verify");
        command.Add(new Option<AbsolutePath>(new[] {"-m", "-machineUrls"}, "Machine url(s) to download"));
        command.Add(new Option<AbsolutePath>(new[] {"-d", "-downloads"}, "Downloads path"));
        command.Add(new Option<AbsolutePath>(new[] {"-o", "-outputs"}, "Outputs path"));
        command.Description = "Installs a modlist, compiles it, installs it again, verifies it";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    public async Task<int> Run(AbsolutePath outputs, AbsolutePath downloads, IEnumerable<string> machineUrls, CancellationToken token)
    {
        foreach (var machineUrl in machineUrls)
        {
            _logger.LogInformation("Installing {MachineUrl}", machineUrl);
            var wabbajackPath = downloads.Combine(machineUrl.Replace("/", "_@@_")).WithExtension(Ext.Wabbajack);
            if (!await DownloadMachineUrl(machineUrl, wabbajackPath, token))
                throw new Exception("Can't download modlist");

            var installPath = outputs.Combine(machineUrl);
            
            var modlist = await StandardInstaller.LoadFromFile(_dtos, wabbajackPath);

            var installer = StandardInstaller.Create(_serviceProvider, new InstallerConfiguration
            {
                Downloads = downloads,
                Install = installPath,
                ModList = modlist,
                Game = modlist.GameType,
                ModlistArchive = wabbajackPath,
                GameFolder = _gameLocator.GameLocation(modlist.GameType)
            });

            var result = await installer.Begin(token);
            if (!result)
            {
                _logger.LogInformation("Error installing {MachineUrl}", machineUrl);
                return 1;
            }


            _logger.LogInformation("Inferring settings");
            var inferedSettings = await _inferencer.InferFromRootPath(installPath);
            if (inferedSettings == null)
            {
                _logger.LogInformation("Error inferencing settings for {MachineUrl}", machineUrl);
                return 2;
            }

            inferedSettings.UseGamePaths = true;
            

            var compiler = MO2Compiler.Create(_serviceProvider, inferedSettings);
            result = await compiler.Begin(token);

            return result ? 0 : 3;

        }

        return 0;
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