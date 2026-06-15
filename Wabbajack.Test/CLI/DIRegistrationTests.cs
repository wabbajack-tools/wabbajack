using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.CLI.Builder;
using Wabbajack.CLI.Verbs;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.CLI.Test;

[ClassConstructor<CliClassConstructor>]
[NotInParallel]
public class DIRegistrationTests
{
    private readonly IServiceProvider _provider;

    public DIRegistrationTests(CLITestFixture fixture)
    {
        _provider = fixture.ServiceProvider;
    }

    [Test]
    public async Task AllRegisteredVerbsCanBeResolved()
    {
        var failedVerbs = CommandLineBuilder.Verbs
            .Where(verbType =>
            {
                try
                {
                    _provider.GetRequiredService(verbType);
                    return false;
                }
                catch
                {
                    return true;
                }
            })
            .ToList();

        await Assert.That(failedVerbs.Count == 0).IsTrue();
    }

    [Test]
    public async Task AllVerbFilesAreRegistered()
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

        await Assert.That(unregistered.Count == 0).IsTrue();
    }

    [Test]
    public async Task AllVerbsHaveDefinitionAndRunMethod()
    {
        foreach (var verbType in CommandLineBuilder.Verbs)
        {
            var definitionField = verbType.GetField("Definition",
                BindingFlags.Public | BindingFlags.Static);
            await Assert.That(definitionField != null).IsTrue();
            await Assert.That(definitionField!.GetValue(null)).IsTypeOf<VerbDefinition>();

            var runMethod = verbType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "Run");
            await Assert.That(runMethod != null).IsTrue();
        }
    }

    [Test]
    public async Task AllVerbDefinitionOptionTypesAreSupported()
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
                await Assert.That(supportedTypes.Contains(option.Type)).IsTrue();
            }
        }
    }

    [Test]
    public async Task NoDuplicateVerbCommandNames()
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

        await Assert.That(duplicates.Count == 0).IsTrue();
    }

    [Test]
    public async Task NoVerbInjectsConcreteGameLocator()
    {
        foreach (var verbType in CommandLineBuilder.Verbs)
        {
            var constructors = verbType.GetConstructors();
            foreach (var ctor in constructors)
            {
                var parameters = ctor.GetParameters();
                foreach (var param in parameters)
                {
                    await Assert.That(param.ParameterType != typeof(GameLocator)).IsTrue();
                }
            }
        }
    }

    [Test]
    public async Task CommandLineBuilderCanBeResolved()
    {
        var builder = _provider.GetRequiredService<CommandLineBuilder>();
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task GameLocatorIsResolvableAsInterface()
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
        await Assert.That(locator).IsNotNull();
    }
}
