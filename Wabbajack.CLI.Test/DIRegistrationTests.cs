using System;
using System.CommandLine;
using System.CommandLine.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit;
using Wabbajack.CLI;
using Wabbajack.CLI.Builder;
using Wabbajack.CLI.Verbs;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths.IO;
using Wabbajack.Server.Lib;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.VFS;
using Xunit;
using Client = Wabbajack.Networking.GitHub.Client;

namespace Wabbajack.CLI.Test;

public class DIRegistrationTests
{
    private IServiceProvider BuildServiceProvider()
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
                services.AddSingleton<TemporaryFileManager>();
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

        return host.Services;
    }

    [Fact]
    public void AllRegisteredVerbsCanBeResolved()
    {
        var provider = BuildServiceProvider();

        var failedVerbs = CommandLineBuilder.Verbs
            .Where(verbType =>
            {
                try
                {
                    provider.GetRequiredService(verbType);
                    return false;
                }
                catch
                {
                    return true;
                }
            })
            .ToList();

        Assert.True(failedVerbs.Count == 0,
            $"Failed to resolve verb types: {string.Join(", ", failedVerbs.Select(t => t.Name))}");
    }

    [Fact]
    public void AllVerbFilesAreRegistered()
    {
        var verbAssembly = typeof(Install).Assembly;
        var verbNamespace = "Wabbajack.CLI.Verbs";

        var verbClassesInAssembly = verbAssembly.GetTypes()
            .Where(t => t.Namespace == verbNamespace
                        && t.IsClass
                        && !t.IsAbstract
                        && !t.IsNested
                        && t.GetField("Definition", BindingFlags.Public | BindingFlags.Static) != null)
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();

        var registeredVerbNames = CommandLineBuilder.Verbs
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();

        var unregistered = verbClassesInAssembly.Except(registeredVerbNames).ToList();

        Assert.True(unregistered.Count == 0,
            $"Verb classes not registered in AddCLIVerbs(): {string.Join(", ", unregistered)}");
    }

    [Fact]
    public void AllVerbsHaveDefinitionAndRunMethod()
    {
        foreach (var verbType in CommandLineBuilder.Verbs)
        {
            var definitionField = verbType.GetField("Definition",
                BindingFlags.Public | BindingFlags.Static);
            Assert.True(definitionField != null,
                $"{verbType.Name} is missing a public static 'Definition' field");
            Assert.IsType<VerbDefinition>(definitionField!.GetValue(null));

            var runMethod = verbType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "Run");
            Assert.True(runMethod != null,
                $"{verbType.Name} is missing a 'Run' method");
        }
    }

    [Fact]
    public void AllVerbDefinitionOptionTypesAreSupported()
    {
        var supportedTypes = new[]
        {
            typeof(string), typeof(int), typeof(Wabbajack.Paths.AbsolutePath),
            typeof(Uri), typeof(bool)
        };

        foreach (var verbType in CommandLineBuilder.Verbs)
        {
            var definitionField = verbType.GetField("Definition", BindingFlags.Public | BindingFlags.Static);
            var definition = (VerbDefinition)definitionField!.GetValue(null)!;

            foreach (var option in definition.Options)
            {
                Assert.True(supportedTypes.Contains(option.Type),
                    $"{verbType.Name} option '--{option.LongOption}' uses unsupported type '{option.Type.Name}'. " +
                    $"Supported: {string.Join(", ", supportedTypes.Select(t => t.Name))}");
            }
        }
    }

    [Fact]
    public void NoDuplicateVerbCommandNames()
    {
        var names = CommandLineBuilder.Verbs
            .Select(verbType =>
            {
                var field = verbType.GetField("Definition", BindingFlags.Public | BindingFlags.Static);
                var def = (VerbDefinition)field!.GetValue(null)!;
                return (verbType.Name, def.Name);
            })
            .ToList();

        var duplicates = names
            .GroupBy(n => n.Item2)
            .Where(g => g.Count() > 1)
            .Select(g => $"'{g.Key}' used by: {string.Join(", ", g.Select(x => x.Item1))}")
            .ToList();

        Assert.True(duplicates.Count == 0,
            $"Duplicate command names: {string.Join("; ", duplicates)}");
    }

    [Fact]
    public void NoVerbInjectsConcreteGameLocator()
    {
        foreach (var verbType in CommandLineBuilder.Verbs)
        {
            var constructors = verbType.GetConstructors();
            foreach (var ctor in constructors)
            {
                var parameters = ctor.GetParameters();
                foreach (var param in parameters)
                {
                    Assert.True(param.ParameterType != typeof(GameLocator),
                        $"{verbType.Name} constructor injects concrete GameLocator " +
                        $"(parameter '{param.Name}'). Use IGameLocator instead.");
                }
            }
        }
    }

    [Fact]
    public void CommandLineBuilderCanBeResolved()
    {
        var provider = BuildServiceProvider();
        var builder = provider.GetRequiredService<CommandLineBuilder>();
        Assert.NotNull(builder);
    }

    [Fact]
    public void GameLocatorIsResolvableAsInterface()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
        services.AddOSIntegrated(o =>
        {
            o.UseLocalCache = true;
            o.UseStubbedGameFolders = true;
        });

        var provider = services.BuildServiceProvider();

        var locator = provider.GetRequiredService<IGameLocator>();
        Assert.NotNull(locator);
    }
}
