using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.CLI.Console;
using Wabbajack.Compiler;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
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
    private readonly IConsoleRenderer _console;
    private readonly StatusUpdateBridge _statusBridge;

    public Compile(ILogger<Compile> logger, Client wjClient, DownloadDispatcher dispatcher, DTOSerializer dtos,
        FileHashCache cache, GameLocator gameLocator, IServiceProvider serviceProvider, CompilerSettingsInferencer inferencer,
        IConsoleRenderer console, StatusUpdateBridge statusBridge)
    {
        _logger = logger;
        _wjClient = wjClient;
        _dispatcher = dispatcher;
        _dtos = dtos;
        _serviceProvider = serviceProvider;
        _cache = cache;
        _gameLocator = gameLocator;
        _inferencer = inferencer;
        _console = console;
        _statusBridge = statusBridge;
    }

    public static VerbDefinition Definition = new("compile", "Compiles a modlist",
    new[]
    {
        new OptionDefinition(typeof(AbsolutePath), "i", "installPath", "Install Path"),
        new OptionDefinition(typeof(AbsolutePath), "o", "outputPath", "OutputPath")
    });

    public async Task<int> Run(AbsolutePath installPath, AbsolutePath outputPath, CancellationToken token)
    {
        _console.Info("Inferring compiler settings...");
        var inferredSettings = await _inferencer.InferFromRootPath(installPath);
        if (inferredSettings == null)
        {
            _console.Error("Failed to infer compiler settings from the install path");
            return 2;
        }

        inferredSettings.UseGamePaths = true;

        if (outputPath.DirectoryExists())
        {
            inferredSettings.OutputFile = outputPath.Combine(inferredSettings.OutputFile.FileName);
        }

        _console.Info($"Compiling modlist: {inferredSettings.ModListName}");
        _console.Info($"Output: {inferredSettings.OutputFile}");

        var compiler = MO2Compiler.Create(_serviceProvider, inferredSettings);

        var result = await _statusBridge.WithProgressAsync(
            $"Compiling {inferredSettings.ModListName}",
            async onStatusUpdate =>
            {
                compiler.OnStatusUpdate += (_, status) => onStatusUpdate(status);
                return await compiler.Begin(token);
            },
            token);

        if (result)
        {
            _console.Success($"Compilation complete: {inferredSettings.OutputFile}");
            return 0;
        }

        _console.Error("Compilation failed");
        return 3;
    }
}