using System;
using System.CommandLine;
using System.CommandLine.IO;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit;
using Wabbajack.CLI.Browser;
using Wabbajack.CLI.TypeConverters;
using Wabbajack.CLI.Verbs;
using Wabbajack.DTOs.GitHub;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Server.Lib;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.VFS;
using Client = Wabbajack.Networking.GitHub.Client;

namespace Wabbajack.CLI;

internal class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        TypeDescriptor.AddAttributes(typeof(AbsolutePath),
            new TypeConverterAttribute(typeof(AbsolutePathTypeConverter)));
        TypeDescriptor.AddAttributes(typeof(List),
            new TypeConverterAttribute(typeof(ModListCategoryConverter)));
        
        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices((host, services) =>
            {
                services.AddSingleton(new JsonSerializerOptions());
                services.AddSingleton<HttpClient, HttpClient>();
                services.AddSingleton<IHttpDownloader, SingleThreadedDownloader>();
                services.AddSingleton<IConsole, SystemConsole>();
                services.AddSingleton<CommandLineBuilder, CommandLineBuilder>();
                services.AddSingleton<TemporaryFileManager>();
                services.AddSingleton<FileExtractor.FileExtractor>();
                services.AddSingleton(new VFSCache(KnownFolders.EntryPoint.Combine("vfscache.sqlite")));
                services.AddSingleton(new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount});
                services.AddSingleton<Client>();
                services.AddSingleton<Networking.WabbajackClientApi.Client>();
                services.AddSingleton(s => new GitHubClient(new ProductHeaderValue("wabbajack")));
                services.AddSingleton<VerbRegistrar>();

                services.AddOSIntegrated();
                services.AddServerLib();

                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                services.AddSingleton<BrowserHost>();

                services.AddTransient<Context>();
                
                services.AddSingleton<Encrypt>();
                services.AddSingleton<HashFile>();
                services.AddSingleton<DownloadCef>();
                services.AddSingleton<Decrypt>();
                services.AddSingleton<DownloadUrl>();
                services.AddSingleton<ForceHeal>();
                services.AddSingleton<MirrorFile>();
                services.AddSingleton<NexusLogin>();
                services.AddSingleton<SteamDownloadFile>();
                services.AddSingleton<SteamLogin>();
                services.AddSingleton<UploadToNexus>();
                services.AddSingleton<ValidateLists>();
                services.AddSingleton<VfsIndexFolder>();

                services.AddSingleton<IUserInterventionHandler, UserInterventionHandler>();
            }).Build();

        var service = host.Services.GetRequiredService<CommandLineBuilder>();
        
        var reg = host.Services.GetRequiredService<VerbRegistrar>();
        
        reg.Register<Decrypt>(Decrypt.MakeCommand);
        reg.Register<DownloadCef>(DownloadCef.MakeCommand);
        
        reg.Register<DownloadUrl>(DownloadUrl.MakeCommand);
        reg.Register<Encrypt>(Encrypt.MakeCommand);
        reg.Register<HashFile>(HashFile.MakeCommand);
        reg.Register<ForceHeal>(ForceHeal.MakeCommand);
        reg.Register<MirrorFile>(MirrorFile.MakeCommand);
        reg.Register<SteamDownloadFile>(SteamDownloadFile.MakeCommand);
        reg.Register<SteamLogin>(SteamLogin.MakeCommand);
        reg.Register<UploadToNexus>(UploadToNexus.MakeCommand);
        reg.Register<ValidateLists>(ValidateLists.MakeCommand);
        reg.Register<VfsIndexFolder>(VfsIndexFolder.MakeCommand);
        reg.Register<NexusLogin>(NexusLogin.MakeCommand);
        
        return await service.Run(args);
    }
}