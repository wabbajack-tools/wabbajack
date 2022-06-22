using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using cesi.DTOs;
using CouchDB.Driver;
using CouchDB.Driver.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Nettle;
using Nettle.Compiler;
using Newtonsoft.Json;
using Octokit;
using Wabbajack.BuildServer;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.GitHub;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.Server.DataModels;
using Wabbajack.Server.Extensions;
using Wabbajack.Server.Services;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths.IO;
using Wabbajack.Server.DTOs;
using Wabbajack.VFS;
using YamlDotNet.Serialization.NamingConventions;
using Client = Wabbajack.Networking.GitHub.Client;

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
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ApiKeyAuthenticationOptions.DefaultScheme;
                options.DefaultChallengeScheme = ApiKeyAuthenticationOptions.DefaultScheme;
            })
            .AddApiKeySupport(options => { });

        services.Configure<FormOptions>(x =>
        {
            x.ValueLengthLimit = int.MaxValue;
            x.MultipartBodyLengthLimit = int.MaxValue;
        });

        services.AddSingleton<AppSettings>();
        services.AddSingleton<QuickSync>();
        services.AddSingleton<GlobalInformation>();
        services.AddSingleton<DiscordWebHook>();
        services.AddSingleton<Metrics>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<AuthorFiles>();
        services.AddSingleton<AuthorKeys>();
        services.AddSingleton<Client>();
        services.AddSingleton<NexusCacheManager>();
        services.AddSingleton<NexusApi>();
        services.AddSingleton<DiscordBackend>();
        services.AddSingleton<TarLog>();
        services.AddAllSingleton<IHttpDownloader, SingleThreadedDownloader>();
        services.AddDownloadDispatcher(useLoginDownloaders:false, useProxyCache:false);
        services.AddTransient(s =>
        {
            var settings = s.GetRequiredService<AppSettings>();
            return new TemporaryFileManager(settings.TempPath.Combine(Environment.ProcessId + "_" + Guid.NewGuid()));
        });
            
        services.AddAllSingleton<ITokenProvider<WabbajackApiState>, WabbajackApiTokenProvider>();
        services.AddAllSingleton<IResource, IResource<DownloadDispatcher>>(s => new Resource<DownloadDispatcher>("Downloads", 12));
        services.AddAllSingleton<IResource, IResource<FileHashCache>>(s => new Resource<FileHashCache>("File Hashing", 12));
        services.AddAllSingleton<IResource, IResource<Wabbajack.Networking.WabbajackClientApi.Client>>(s => 
            new Resource<Wabbajack.Networking.WabbajackClientApi.Client>("Wabbajack Client", 4));

        services.AddSingleton(s => 
            new FileHashCache(KnownFolders.AppDataLocal.Combine("Wabbajack", "GlobalHashCache.sqlite"),
                s.GetService<IResource<FileHashCache>>()!));

        services.AddAllSingleton<ITokenProvider<NexusApiState>, NexusApiTokenProvider>();
        services.AddAllSingleton<IResource, IResource<HttpClient>>(s => new Resource<HttpClient>("Web Requests", 12));
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

        
        services.AddResponseCaching();
        services.AddSingleton(s =>
        {
            var settings = s.GetService<AppSettings>()!;
            if (string.IsNullOrWhiteSpace(settings.GitHubKey)) 
                return new GitHubClient(new ProductHeaderValue("wabbajack"));
            
            var creds = new Credentials(settings.GitHubKey);
            return new GitHubClient(new ProductHeaderValue("wabbajack")) {Credentials = creds};
        });
        services.AddDTOSerializer();
        services.AddDTOConverters();
        services.AddResponseCompression(options =>
        {
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = new[] {"application/json"};
        });
        
        // CouchDB
        services.AddSingleton(s =>
        {
            var settings = s.GetRequiredService<AppSettings>().CesiDB;
            var client = new CouchClient(settings.Endpoint, b =>
            {
                b.UseBasicAuthentication(settings.Username, settings.Password);
                b.SetPropertyCase(PropertyCaseType.None);
                b.SetJsonNullValueHandling(NullValueHandling.Ignore);
            });
            return client.GetDatabase<Analyzed>(settings.Database);
        });
        
        services.AddSingleton(s =>
        {
            var settings = s.GetRequiredService<AppSettings>().MetricsDB;
            var client = new CouchClient(settings.Endpoint, b =>
            {
                b.UseBasicAuthentication(settings.Username, settings.Password);
                b.SetPropertyCase(PropertyCaseType.None);
                b.SetJsonNullValueHandling(NullValueHandling.Ignore);
            });
            return client.GetDatabase<Metric>(settings.Database);
        });

        services.AddMvc();
        services
            .AddControllers()
            .AddJsonOptions(j =>
            {
                j.JsonSerializerOptions.PropertyNamingPolicy = null;
                j.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
            });

        NettleEngine.GetCompiler().RegisterWJFunctions();
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
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseResponseCompression();

        app.UseService<DiscordWebHook>();

        app.UseResponseCaching();

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

        app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(Directory.GetCurrentDirectory(), "public")),
            StaticFileOptions = {ServeUnknownFileTypes = true}
        });

        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

        // Trigger the internal update code
        app.ApplicationServices.GetRequiredService<NexusCacheManager>();
        app.ApplicationServices.GetRequiredService<DiscordBackend>();
    }
}