using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Compiler;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Logins;
using Wabbajack.Installer;
using Wabbajack.Networking.Discord;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using Wabbajack.VFS;
using ApplicationInfo = Wabbajack.DTOs.ApplicationInfo;

namespace Wabbajack.Services.OSIntegrated
{
    public static class ServiceExtensions
    {

        public class OSIntegratedOptions
        {
            public bool UseLocalCache { get; set; } = false;
            public bool UseStubbedGameFolders { get; set; } = false;
        }
        /// <summary>
        /// Adds variants of services that integrate into global OS services. These are not testing
        /// variants or services that require Environment variables. These are the "full fat" services.
        /// </summary>
        /// <returns></returns>
        public static IServiceCollection AddOSIntegrated(this IServiceCollection service, Action<OSIntegratedOptions>? cfn = null)
        {
            var options = new OSIntegratedOptions();
            cfn?.Invoke(options);

            service.AddTransient(s => new TemporaryFileManager(KnownFolders.EntryPoint.Combine("temp", Guid.NewGuid().ToString())));
            
            service.AddSingleton(s => options.UseLocalCache ? 
                new FileHashCache(s.GetService<TemporaryFileManager>()!.CreateFile().Path) 
                : new FileHashCache(KnownFolders.AppDataLocal.Combine("Wabbajack", "GlobalHashCache.sqlite")));
            
            service.AddSingleton(s => options.UseLocalCache ? 
                new VFSCache(s.GetService<TemporaryFileManager>()!.CreateFile().Path)
                : new VFSCache(KnownFolders.EntryPoint.Combine("GlobalVFSCache3.sqlite")));
            
            service.AddSingleton<IBinaryPatchCache>(s => options.UseLocalCache ? 
                new BinaryPatchCache(s.GetService<TemporaryFileManager>()!.CreateFile().Path)
                : new BinaryPatchCache(KnownFolders.EntryPoint.Combine("patchCache.sqlite")));
            
            service.AddSingleton(new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount});
            service.AddAllSingleton<IResource, IResource<DownloadDispatcher>>(s => new Resource<DownloadDispatcher>(12));
            service.AddAllSingleton<IResource, IResource<HttpClient>>(s => new Resource<HttpClient>(12));
            service.AddAllSingleton<IResource, IResource<Context>>(s => new Resource<Context>(12));
            service.AddAllSingleton<IResource, IResource<FileExtractor.FileExtractor>>(s =>
                new Resource<FileExtractor.FileExtractor>(12));

            service.AddSingleton<LoggingRateLimiterReporter>();

            service.AddScoped<Context>();
            service.AddSingleton<FileExtractor.FileExtractor>();

            // Networking
            service.AddSingleton<HttpClient>();
            service.AddAllSingleton<IHttpDownloader, SingleThreadedDownloader>();
            service.AddSingleton<Configuration>();

            service.AddSingleton<Client>();
            service.AddSingleton<WriteOnlyClient>();
            
            // Token Providers
            service.AddAllSingleton<ITokenProvider<NexusApiState>, NexusApiTokenProvider>();
            service.AddAllSingleton<ITokenProvider<LoversLabLoginState>, EncryptedJsonTokenProvider<LoversLabLoginState>, LoversLabTokenProvider>();
            service.AddAllSingleton<ITokenProvider<VectorPlexusLoginState>, EncryptedJsonTokenProvider<VectorPlexusLoginState>, VectorPlexusTokenProvider>();

            service.AddAllSingleton<ITokenProvider<WabbajackApiState>, WabbajackApiTokenProvider>();

            service
                .AddAllSingleton<ITokenProvider<Dictionary<Channel, DiscordWebHookSetting>>,
                    EncryptedJsonTokenProvider<Dictionary<Channel, DiscordWebHookSetting>>, DiscordTokenProvider>();

            service.AddAllSingleton<NexusApi, ProxiedNexusApi>();
            service.AddDownloadDispatcher();
            
            if (options.UseStubbedGameFolders)
            {
                service.AddAllSingleton<IGameLocator, StubbedGameLocator>();
            }
            else
            {
                service.AddAllSingleton<IGameLocator, GameLocator>();
                
            }
            
            // Installer/Compiler Configuration
            service.AddScoped<InstallerConfiguration>();
            service.AddScoped<StandardInstaller>();
            service.AddScoped<MO2CompilerSettings>();
            service.AddScoped<MO2Compiler>();

            // Application Info
            var version =
                $"{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Patch}{ThisAssembly.Git.SemVer.DashLabel}";
            service.AddSingleton(s => new ApplicationInfo
            {
                ApplicationSlug = "Wabbajack",
                ApplicationName = Environment.ProcessPath?.ToAbsolutePath().FileName.ToString() ?? "Wabbajack",
                ApplicationSha = ThisAssembly.Git.Sha,
                Platform = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                OperatingSystemDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                RuntimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                OSVersion = Environment.OSVersion.VersionString,
                Version = version
            });
            return service;
        }
    }
}