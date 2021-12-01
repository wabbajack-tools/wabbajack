using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Octokit;
using Wabbajack.BuildServer;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.GitHub;
using Wabbajack.Server.DataModels;
using Wabbajack.Server.Services;

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
        services.AddSingleton<Watchdog>();
        services.AddSingleton<Metrics>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<AuthorFiles>();
        services.AddSingleton<AuthorKeys>();
        services.AddSingleton<Client>();
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

        services.AddMvc();
        services.AddControllers()
            .AddNewtonsoftJson(o => { o.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore; });
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
        app.UseService<Watchdog>();

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
    }
}