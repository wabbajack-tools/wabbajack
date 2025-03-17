using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
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

public class InstallCompileInstallVerify
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

    public static VerbDefinition Definition = new("install-compile-install-verify",
        "Installs a modlist, compiles it, installs it again, verifies it", new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "m", "machineUrl", "Machine url(s) to download"),
            new OptionDefinition(typeof(AbsolutePath), "d", "downloads", "Downloads path"),
            new OptionDefinition(typeof(AbsolutePath), "o", "outputs", "Output paths")
        });
    
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

            var result = await installer.Begin(token) == InstallResult.Succeeded;
            if (!result)
            {
                _logger.LogInformation("Error installing {MachineUrl}", machineUrl);
                return 1;
            }


            _logger.LogInformation("Inferring settings");
            var inferredSettings = await _inferencer.InferFromRootPath(installPath);
            if (inferredSettings == null)
            {
                _logger.LogInformation("Error inferencing settings for {MachineUrl}", machineUrl);
                return 2;
            }

            inferredSettings.UseGamePaths = true;
            

            var compiler = MO2Compiler.Create(_serviceProvider, inferredSettings);
            result = await compiler.Begin(token);
            if (!result)
                return 3;
            
            
            var installPath2 = outputs.Combine("verify_list");

            var comparison = await StandardInstaller.LoadFromFile(_dtos, wabbajackPath);
            
            var modlist2 = await StandardInstaller.LoadFromFile(_dtos, inferredSettings.OutputFile);
            if (CompareModlists(comparison, modlist2))
                return 3;

            var installer2 = StandardInstaller.Create(_serviceProvider, new InstallerConfiguration
            {
                Downloads = downloads,
                Install = installPath2,
                ModList = modlist2,
                Game = modlist2.GameType,
                ModlistArchive = inferredSettings.OutputFile,
                GameFolder = _gameLocator.GameLocation(modlist2.GameType)
            });

            result = await installer2.Begin(token) == InstallResult.Succeeded;
            if (!result)
            {
                _logger.LogInformation("Error installing recompiled {MachineUrl}", machineUrl);
                return 1;
            }

        }

        return 0;
    }

    private bool CompareModlists(ModList a, ModList b)
    {
        var aDirectives = a.Directives.ToDictionary(d => d.To);
        var bDirectives = b.Directives.ToDictionary(d => d.To);

        var found = false;
        foreach (var missing in aDirectives.Where(ad => !bDirectives.ContainsKey(ad.Key)))
        {
            if (missing.Key.Extension == Ext.Meta)
                continue;
            _logger.LogWarning("File {To} is missing in recompiled list", missing.Key);
            found = true;
        }
        
        foreach (var missing in bDirectives.Where(bd => !aDirectives.ContainsKey(bd.Key)))
        {
            _logger.LogWarning("File {To} is missing in original list", missing.Key);
            found = true;
        }

        return found;
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