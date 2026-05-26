using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.VFS;

namespace Wabbajack.CLI.Verbs;

public class Compile
{
    private readonly ILogger<Compile> _logger;
    private readonly IModlistPublisher _publisher;
    private readonly DownloadDispatcher _dispatcher;
    private readonly DTOSerializer _dtos;
    private readonly IServiceProvider _serviceProvider;
    private readonly FileHashCache _cache;
    private readonly IGameLocator _gameLocator;
    private readonly CompilerSettingsInferencer _inferencer;

    public Compile(ILogger<Compile> logger, IModlistPublisher publisher, DownloadDispatcher dispatcher,
        DTOSerializer dtos, FileHashCache cache, IGameLocator gameLocator, IServiceProvider serviceProvider,
        CompilerSettingsInferencer inferencer)
    {
        _logger = logger;
        _publisher = publisher;
        _dispatcher = dispatcher;
        _dtos = dtos;
        _serviceProvider = serviceProvider;
        _cache = cache;
        _gameLocator = gameLocator;
        _inferencer = inferencer;
    }

    public static VerbDefinition Definition = new("compile", "Compiles a modlist",
    new[]
    {
        new OptionDefinition(typeof(AbsolutePath), "i", "installPath", "Install Path"),
        new OptionDefinition(typeof(AbsolutePath), "o", "outputPath", "OutputPath"),
        new OptionDefinition(typeof(bool), "p", "publish", "Publish the compiled modlist to the Wabbajack CDN")
    });

    public async Task<int> Run(AbsolutePath installPath, AbsolutePath outputPath,
        bool publish, CancellationToken token)
    {
        var inferredSettings = await _inferencer.LoadOrInferFromRootPath(installPath, _dtos);
        if (inferredSettings == null)
        {
            _logger.LogInformation("Error inferencing settings");
            return 2;
        }

        inferredSettings.UseGamePaths = true;

        if(outputPath.DirectoryExists())
        {
            inferredSettings.OutputFile = outputPath.Combine(inferredSettings.OutputFile.FileName);
            _logger.LogInformation("Output file will be in: {outputPath}", inferredSettings.OutputFile);
        }

        var compiler = MO2Compiler.Create(_serviceProvider, inferredSettings);
        var result = await compiler.Begin(token);
        if (!result)
            return 3;

        if (publish)
            return await RunPublish(inferredSettings, token);

        return 0;
    }

    internal async Task<int> RunPublish(CompilerSettings settings, CancellationToken token)
    {
        var metaPath = settings.OutputFile.WithExtension(Ext.Meta).WithExtension(Ext.Json);
        var metadata = _dtos.Deserialize<DownloadMetadata>(await metaPath.ReadAllTextAsync())!;
        _logger.LogInformation("Publishing {MachineUrl} v{Version}", settings.MachineUrl, settings.Version);
        var (_, publishTask) = await _publisher.PublishModlist(
            settings.MachineUrl, settings.Version, settings.OutputFile, metadata);
        await publishTask;
        return 0;
    }
}
