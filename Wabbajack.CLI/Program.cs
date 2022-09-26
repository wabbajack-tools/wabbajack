using System;
using System.CommandLine;
using System.CommandLine.IO;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Targets;
using Octokit;
using Wabbajack.CLI.TypeConverters;
using Wabbajack.CLI.Verbs;
using Wabbajack.DTOs.GitHub;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Server.Lib;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.VFS;
using Client = Wabbajack.Networking.GitHub.Client;

namespace Wabbajack.CLI;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        TypeDescriptor.AddAttributes(typeof(AbsolutePath),
            new TypeConverterAttribute(typeof(AbsolutePathTypeConverter)));
        TypeDescriptor.AddAttributes(typeof(List),
            new TypeConverterAttribute(typeof(ModListCategoryConverter)));

        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureLogging(AddLogging)
            .ConfigureServices((host, services) =>
            {
                services.AddSingleton(new JsonSerializerOptions());
                services.AddSingleton<HttpClient, HttpClient>();
                services.AddSingleton<IHttpDownloader, SingleThreadedDownloader>();
                services.AddSingleton<IConsole, SystemConsole>();
                services.AddSingleton<CommandLineBuilder, CommandLineBuilder>();
                services.AddSingleton<TemporaryFileManager>();
                services.AddSingleton<FileExtractor.FileExtractor>();
                services.AddSingleton(new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount});
                services.AddSingleton<Client>();
                services.AddSingleton<Networking.WabbajackClientApi.Client>();
                services.AddSingleton(s => new GitHubClient(new ProductHeaderValue("wabbajack")));

                services.AddOSIntegrated();
                services.AddServerLib();


                services.AddTransient<Context>();
                services.AddSingleton<IVerb, HashFile>();
                services.AddSingleton<IVerb, VFSIndexFolder>();
                services.AddSingleton<IVerb, Encrypt>();
                services.AddSingleton<IVerb, Decrypt>();
                services.AddSingleton<IVerb, ValidateLists>();
                services.AddSingleton<IVerb, DownloadCef>();
                services.AddSingleton<IVerb, DownloadUrl>();
                services.AddSingleton<IVerb, GenerateMetricsReports>();
                services.AddSingleton<IVerb, ForceHeal>();
                services.AddSingleton<IVerb, MirrorFile>();
                services.AddSingleton<IVerb, SteamLogin>();
                services.AddSingleton<IVerb, SteamAppDumpInfo>();
                services.AddSingleton<IVerb, SteamDownloadFile>();
                services.AddSingleton<IVerb, UploadToNexus>();
                services.AddSingleton<IVerb, ListCreationClubContent>();
                services.AddSingleton<IVerb, ListModlists>();
                services.AddSingleton<IVerb, Extract>();
                services.AddSingleton<IVerb, DumpZipInfo>();
                services.AddSingleton<IVerb, Install>();
                services.AddSingleton<IVerb, Compile>();
                services.AddSingleton<IVerb, InstallCompileInstallVerify>();
                services.AddSingleton<IVerb, HashUrlString>();
                services.AddSingleton<IVerb, DownloadAll>();
                services.AddSingleton<IVerb, ModlistReport>();
                services.AddSingleton<IVerb, ListGames>();

                services.AddSingleton<IUserInterventionHandler, UserInterventionHandler>();
            }).Build();

        var service = host.Services.GetService<CommandLineBuilder>();
        return await service!.Run(args);
    }
    
    private static void AddLogging(ILoggingBuilder loggingBuilder)
    {
        var config = new NLog.Config.LoggingConfiguration();

        var fileTarget = new FileTarget("file")
        {
            FileName = "logs/wabbajack-cli.current.log",
            ArchiveFileName = "logs/wabbajack-cli.{##}.log",
            ArchiveOldFileOnStartup = true,
            MaxArchiveFiles = 10,
            Layout = "${processtime} [${level:uppercase=true}] (${logger}) ${message:withexception=true}",
            Header = "############ Wabbajack log file - ${longdate} ############"
        };

        var consoleTarget = new ConsoleTarget("console")
        {
            Layout = "${processtime} [${level:uppercase=true}] ${message:withexception=true}",
        };
        

        config.AddRuleForAllLevels(fileTarget);
        config.AddRuleForAllLevels(consoleTarget);

        loggingBuilder.ClearProviders();
        loggingBuilder.SetMinimumLevel(LogLevel.Trace);
        loggingBuilder.AddNLog(config);
    }
}