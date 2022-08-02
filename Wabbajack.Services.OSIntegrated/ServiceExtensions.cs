using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Models;
using Wabbajack.Compiler;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Logins;
using Wabbajack.Installer;
using Wabbajack.Networking.BethesdaNet;
using Wabbajack.Networking.Discord;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Networking.Steam;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Services;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using Wabbajack.VFS;
using Wabbajack.VFS.Interfaces;
using Client = Wabbajack.Networking.WabbajackClientApi.Client;

namespace Wabbajack.Services.OSIntegrated;

public static class ServiceExtensions
{
    /// <summary>
    ///     Adds variants of services that integrate into global OS services. These are not testing
    ///     variants or services that require Environment variables. These are the "full fat" services.
    /// </summary>
    /// <returns></returns>
    public static IServiceCollection AddOSIntegrated(this IServiceCollection service,
        Action<OSIntegratedOptions>? cfn = null)
    {
        var options = new OSIntegratedOptions();
        cfn?.Invoke(options);

        var tempBase = KnownFolders.EntryPoint.Combine("temp");
        service.AddTransient(s =>
            new TemporaryFileManager(tempBase.Combine(Environment.ProcessId + "_" + Guid.NewGuid())));

        Task.Run(() => CleanAllTempData(tempBase));

        service.AddSingleton(s => options.UseLocalCache
            ? new FileHashCache(s.GetService<TemporaryFileManager>()!.CreateFile().Path,
                s.GetService<IResource<FileHashCache>>()!)
            : new FileHashCache(KnownFolders.AppDataLocal.Combine("Wabbajack", "GlobalHashCache.sqlite"),
                s.GetService<IResource<FileHashCache>>()!));

        service.AddSingleton<IVfsCache>(s =>
        {
            var diskCache = options.UseLocalCache
                ? new VFSDiskCache(s.GetService<TemporaryFileManager>()!.CreateFile().Path)
                : new VFSDiskCache(KnownFolders.WabbajackAppLocal.Combine("GlobalVFSCache3.sqlite"));
            var cesiCache = new CesiVFSCache(s.GetRequiredService<ILogger<CesiVFSCache>>(),
                s.GetRequiredService<Client>());
            return new FallthroughVFSCache(new IVfsCache[] {diskCache});
        });

        service.AddSingleton<IBinaryPatchCache>(s => options.UseLocalCache
            ? new BinaryPatchCache(s.GetService<TemporaryFileManager>()!.CreateFile().Path)
            : new BinaryPatchCache(KnownFolders.EntryPoint.Combine("patchCache.sqlite")));

        service.AddSingleton(new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount});

        Func<Task<(int MaxTasks, long MaxThroughput)>> GetSettings(IServiceProvider provider, string name)
        {
            return async () =>
            {
                var s = await provider.GetService<ResourceSettingsManager>()!.GetSettings(name);
                return ((int) s.MaxTasks, s.MaxThroughput);
            };
        }
        
        // Settings
        
        service.AddSingleton(s => new Configuration
        {
            EncryptedDataLocation = KnownFolders.WabbajackAppLocal.Combine("encrypted"),
            ModListsDownloadLocation = KnownFolders.EntryPoint.Combine("downloaded_mod_lists"),
            SavedSettingsLocation = KnownFolders.WabbajackAppLocal.Combine("saved_settings"),
            LogLocation = KnownFolders.EntryPoint.Combine("logs"),
            ImageCacheLocation = KnownFolders.WabbajackAppLocal.Combine("image_cache")
        });

        service.AddSingleton<SettingsManager>();
        service.AddSingleton<ResourceSettingsManager>();
        
        // Resources

        service.AddAllSingleton<IResource, IResource<DownloadDispatcher>>(s =>
            new Resource<DownloadDispatcher>("Downloads", GetSettings(s, "Downloads")));

        service.AddAllSingleton<IResource, IResource<HttpClient>>(s => new Resource<HttpClient>("Web Requests", GetSettings(s, "Web Requests")));
        service.AddAllSingleton<IResource, IResource<Context>>(s => new Resource<Context>("VFS", GetSettings(s, "VFS")));
        service.AddAllSingleton<IResource, IResource<FileHashCache>>(s =>
            new Resource<FileHashCache>("File Hashing", GetSettings(s, "File Hashing")));
        service.AddAllSingleton<IResource, IResource<Client>>(s =>
            new Resource<Client>("Wabbajack Client", GetSettings(s, "Wabbajack Client")));
        service.AddAllSingleton<IResource, IResource<FileExtractor.FileExtractor>>(s =>
            new Resource<FileExtractor.FileExtractor>("File Extractor", GetSettings(s, "File Extractor")));

        service.AddAllSingleton<IResource, IResource<ACompiler>>(s =>
            new Resource<ACompiler>("Compiler", GetSettings(s, "Compiler")));
        
        service.AddAllSingleton<IResource, IResource<IInstaller>>(s =>
            new Resource<IInstaller>("Installer", GetSettings(s, "Installer")));
        
        service.AddAllSingleton<IResource, IResource<IUserInterventionHandler>>(s =>
            new Resource<IUserInterventionHandler>("User Intervention", 1));

        service.AddSingleton<LoggingRateLimiterReporter>();

        service.AddScoped<Context>();
        service.AddSingleton<FileExtractor.FileExtractor>();

        service.AddSingleton<ModListDownloadMaintainer>();

        // Networking
        service.AddSingleton<HttpClient>();
        service.AddAllSingleton<IHttpDownloader, SingleThreadedDownloader>();

        service.AddSteam();
            
        service.AddSingleton<Client>();
        service.AddSingleton<WriteOnlyClient>();
        service.AddBethesdaNet();

        // Token Providers
        service.AddAllSingleton<ITokenProvider<NexusApiState>, EncryptedJsonTokenProvider<NexusApiState>, NexusApiTokenProvider>();
        service.AddAllSingleton<ITokenProvider<BethesdaNetLoginState>, EncryptedJsonTokenProvider<BethesdaNetLoginState>, BethesdaNetTokenProvider>();
        service
            .AddAllSingleton<ITokenProvider<LoversLabLoginState>, EncryptedJsonTokenProvider<LoversLabLoginState>,
                LoversLabTokenProvider>();
        service
            .AddAllSingleton<ITokenProvider<VectorPlexusLoginState>, EncryptedJsonTokenProvider<VectorPlexusLoginState>,
                VectorPlexusTokenProvider>();

        service
            .AddAllSingleton<ITokenProvider<SteamLoginState>, EncryptedJsonTokenProvider<SteamLoginState>,
                SteamTokenProvider>();

        service.AddAllSingleton<ITokenProvider<WabbajackApiState>, WabbajackApiTokenProvider>();

        service
            .AddAllSingleton<ITokenProvider<Dictionary<Channel, DiscordWebHookSetting>>,
                EncryptedJsonTokenProvider<Dictionary<Channel, DiscordWebHookSetting>>, DiscordTokenProvider>();

        service.AddAllSingleton<NexusApi, ProxiedNexusApi>();
        service.AddDownloadDispatcher();

        if (options.UseStubbedGameFolders)
            service.AddAllSingleton<IGameLocator, StubbedGameLocator>();
        else
            service.AddAllSingleton<IGameLocator, GameLocator>();

        // Installer/Compiler Configuration
        service.AddScoped<InstallerConfiguration>();
        service.AddScoped<StandardInstaller>();
        service.AddScoped<CompilerSettings>();
        service.AddScoped<MO2Compiler>();
        service.AddSingleton<CompilerSettingsInferencer>();

        // Application Info
        var version =
            $"{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Patch}{ThisAssembly.Git.SemVer.DashLabel}";
        service.AddSingleton(s => new ApplicationInfo
        {
            ApplicationSlug = "Wabbajack",
            ApplicationName = Environment.ProcessPath?.ToAbsolutePath().FileName.ToString() ?? "Wabbajack",
            ApplicationSha = ThisAssembly.Git.Sha,
            Platform = RuntimeInformation.ProcessArchitecture.ToString(),
            OperatingSystemDescription = RuntimeInformation.OSDescription,
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            OSVersion = Environment.OSVersion.VersionString,
            Version = version
        });
        

        
        return service;
    }

    private static void CleanAllTempData(AbsolutePath path)
    {
        // Get directories first and cache them, this freezes the directories were looking at
        // so any new ones don't show up in the middle of our deletes.
        
        var dirs = path.EnumerateDirectories().ToList();
        var processIds = Process.GetProcesses().Select(p => p.Id).ToHashSet();
        foreach (var dir in dirs)
        {
            var name = dir.FileName.ToString().Split("_");
            if (!int.TryParse(name[0], out var processId)) continue;
            if (processIds.Contains(processId)) continue;
            
            try
            {
                dir.DeleteDirectory();
            }
            catch (Exception)
            {
                // ignored
            }
        }
        
    }

    public class OSIntegratedOptions
    {
        public bool UseLocalCache { get; set; } = false;
        public bool UseStubbedGameFolders { get; set; } = false;
    }
}