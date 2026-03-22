using System;
using System.CommandLine;
using System.CommandLine.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit;
using Wabbajack.CLI.Builder;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Http;
using Wabbajack.Paths.IO;
using Wabbajack.Server.Lib;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.VFS;
using Xunit;
using Client = Wabbajack.Networking.GitHub.Client;

namespace Wabbajack.CLI.Test;

/// <summary>
/// Shared fixture that builds the DI container once for all CLI tests.
/// This avoids duplicate verb registrations in the static CommandLineBuilder._commands list.
/// </summary>
public class CLITestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }

    public CLITestFixture()
    {
        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices((host, services) =>
            {
                services.AddSingleton(new JsonSerializerOptions());
                services.AddSingleton<HttpClient, HttpClient>();
                services.AddResumableHttpDownloader();
                services.AddSingleton<IConsole, SystemConsole>();
                services.AddSingleton<CommandLineBuilder, CommandLineBuilder>();
                services.AddSingleton<TemporaryFileManager>();
                services.AddSingleton<FileExtractor.FileExtractor>();
                services.AddSingleton(new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount });
                services.AddSingleton<Client>();
                services.AddSingleton<Networking.WabbajackClientApi.Client>();
                services.AddSingleton(s => new GitHubClient(new ProductHeaderValue("wabbajack")));
                services.AddSingleton<MegaApiClient>();
                services.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
                services.AddOSIntegrated(o =>
                {
                    o.UseLocalCache = true;
                    o.UseStubbedGameFolders = true;
                });
                services.AddServerLib();
                services.AddTransient<Context>();
                services.AddSingleton<CommandLineBuilder>();
                services.AddCLIVerbs();
            }).Build();

        ServiceProvider = host.Services;
    }

    public void Dispose()
    {
    }
}

[CollectionDefinition("CLI")]
public class CLITestCollection : ICollectionFixture<CLITestFixture>
{
}
