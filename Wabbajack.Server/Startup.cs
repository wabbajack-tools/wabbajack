using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wabbajack.BuildServer;
using Wabbajack.Configuration;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.ModDB;
using Wabbajack.Downloaders.VerificationCache;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using Wabbajack.Paths.IO;
using Wabbajack.Server.Controllers;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.VFS;
using Client = Wabbajack.Networking.GitHub.Client;
using SettingsManager = Wabbajack.Services.OSIntegrated.SettingsManager;

namespace Wabbajack.Server;

public class TestStartup : Startup
{
    public TestStartup(IConfiguration configuration) : base(configuration)
    {
    }
}

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<FormOptions>(x =>
        {
            x.ValueLengthLimit = int.MaxValue;
            x.MultipartBodyLengthLimit = int.MaxValue;
        });

        services.AddSingleton<AppSettings>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<Client>();
        services.AddSingleton<NexusApi>();
        services.AddAllSingleton<IHttpDownloader, SingleThreadedDownloader>();
        services.AddDownloadDispatcher(useLoginDownloaders:false, useProxyCache:false);
        services.AddSingleton<IAmazonS3>(s =>
        {
            var appSettings = s.GetRequiredService<AppSettings>();
            var settings = new BasicAWSCredentials(appSettings.ProxyStorage.AccessKey,
                appSettings.ProxyStorage.SecretKey);
            return new AmazonS3Client(settings, new AmazonS3Config
            {
                ServiceURL = appSettings.ProxyStorage.ServiceURL,
            });
        });
        services.AddSingleton<IVerificationCache, NullCache>();
            
        services.AddAllSingleton<ITokenProvider<WabbajackApiState>, WabbajackApiTokenProvider>();
        services.AddAllSingleton<IResource, IResource<Proxy>>(s => new Resource<Proxy>("Proxy", 8));
        services.AddAllSingleton<IResource, IResource<DownloadDispatcher>>(s => new Resource<DownloadDispatcher>("Downloads", 12));
        services.AddAllSingleton<IResource, IResource<FileHashCache>>(s => new Resource<FileHashCache>("File Hashing", 12));
        services.AddAllSingleton<IResource, IResource<Wabbajack.Networking.WabbajackClientApi.Client>>(s => 
            new Resource<Wabbajack.Networking.WabbajackClientApi.Client>("Wabbajack Client", 4));

        services.AddSingleton(s => 
            new FileHashCache(KnownFolders.AppDataLocal.Combine("Wabbajack", "GlobalHashCache.sqlite"),
                s.GetService<IResource<FileHashCache>>()!));

        services.AddAllSingleton<ITokenProvider<NexusApiState>, NexusApiTokenProvider>();
        services.AddAllSingleton<IResource, IResource<HttpClient>>(s => new Resource<HttpClient>("Web Requests", 12));
        
        services.AddAllSingleton<ITokenProvider<MegaToken>, EncryptedJsonTokenProvider<MegaToken>, MegaTokenProvider>();
        
        // Application Info
        
        var version =
            $"{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Patch}{ThisAssembly.Git.SemVer.DashLabel}";
        services.AddSingleton(s => new ApplicationInfo
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

        services.AddDTOSerializer();
        services.AddDTOConverters();
        services.AddSingleton<TemporaryFileManager>();
        services.AddRateLimiter(_ => _
            .AddConcurrencyLimiter("fixed", options =>
            {
                options.PermitLimit = 2;
                options.QueueLimit = 4;
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            }));
        
        services.AddSingleton(s => new Wabbajack.Services.OSIntegrated.Configuration
        {
            EncryptedDataLocation = KnownFolders.WabbajackAppLocal.Combine("encrypted"),
            ModListsDownloadLocation = KnownFolders.EntryPoint.Combine("downloaded_mod_lists"),
            SavedSettingsLocation = KnownFolders.WabbajackAppLocal.Combine("saved_settings"),
            LogLocation = KnownFolders.LauncherAwarePath.Combine("logs"),
            ImageCacheLocation = KnownFolders.WabbajackAppLocal.Combine("image_cache")
        });


        services.AddSingleton<SettingsManager>();
        services.AddSingleton<MainSettings>(s => Wabbajack.Services.OSIntegrated.ServiceExtensions.GetAppSettings(s, MainSettings.SettingsFileName));
        
        services.AddMvc();
        services
            .AddControllers()
            .AddJsonOptions(j =>
            {
                j.JsonSerializerOptions.PropertyNamingPolicy = null;
                j.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
            });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

        app.UseDeveloperExceptionPage();

        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".rar"] = "application/x-rar-compressed";
        provider.Mappings[".7z"] = "application/x-7z-compressed";
        provider.Mappings[".zip"] = "application/zip";
        provider.Mappings[".wabbajack"] = "application/zip";

        app.UseRouting();
        app.UseRateLimiter();

        app.Use(next =>
        {
            return async context =>
            {
                var stopWatch = new Stopwatch();
                stopWatch.Start();
                context.Response.OnStarting(() =>
                {
                    stopWatch.Stop();
                    var headers = context.Response.Headers;
                    headers.Add("Access-Control-Allow-Origin", "*");
                    headers.Add("Access-Control-Allow-Methods", "POST, GET");
                    headers.Add("Access-Control-Allow-Headers", "Accept, Origin, Content-type");
                    headers.Add("X-ResponseTime-Ms", stopWatch.ElapsedMilliseconds.ToString());
                    if (!headers.ContainsKey("Cache-Control"))
                        headers.Add("Cache-Control", "no-cache");
                    return Task.CompletedTask;
                });
                await next(context);
            };
        });

        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
    }
}