using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Compiler;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.VFS;

namespace Wabbajack.CLI.Verbs;

public class Compile
{
    private readonly ILogger<Compile> _logger;
    private readonly Client _wjClient;
    private readonly DownloadDispatcher _dispatcher;
    private readonly DTOSerializer _dtos;
    private readonly IServiceProvider _serviceProvider;
    private readonly FileHashCache _cache;
    private readonly GameLocator _gameLocator;
    private readonly CompilerSettingsInferencer _inferencer;

    public Compile(ILogger<Compile> logger, Client wjClient, DownloadDispatcher dispatcher, DTOSerializer dtos, 
        FileHashCache cache, GameLocator gameLocator, IServiceProvider serviceProvider, CompilerSettingsInferencer inferencer)
    {
        _logger = logger;
        _wjClient = wjClient;
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
        new OptionDefinition(typeof(AbsolutePath), "o", "outputPath", "OutputPath")
    });
    public async Task<int> Run(AbsolutePath installPath, AbsolutePath outputPath,
        CancellationToken token)
    {
        _logger.LogInformation("Inferring settings");
        var inferredSettings = await _inferencer.InferFromRootPath(installPath);
        if (inferredSettings == null)
        {
            _logger.LogInformation("Error inferencing settings");
            return 2;
        }

        inferredSettings.UseGamePaths = true;
        
        var compiler = MO2Compiler.Create(_serviceProvider, inferredSettings);
        var result = await compiler.Begin(token);
        if (!result)
            return result ? 0 : 3;

        return 0;
    }
}