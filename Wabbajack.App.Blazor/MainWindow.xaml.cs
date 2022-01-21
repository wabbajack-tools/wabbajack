using System;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Blazor.Models;
using Wabbajack.App.Blazor.Utility;
using Wabbajack.Common;
using Wabbajack.Installer;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Blazor;

public partial class MainWindow
{
    private readonly ILogger<MainWindow>         _logger;
    private readonly LoggerProvider              _loggerProvider;
    private readonly SystemParametersConstructor _systemParams;

    public MainWindow(ILogger<MainWindow> logger, IServiceProvider serviceProvider, LoggerProvider loggerProvider,
                      SystemParametersConstructor systemParams)
    {
        _logger         = logger;
        _loggerProvider = loggerProvider;
        _systemParams   = systemParams;
        InitializeComponent();
        BlazorWebView.Services = serviceProvider;

        try
        {
            // TODO: [Low] Not sure how to set this up.
            //_logger.LogInformation("Wabbajack Build - {Sha}", ThisAssembly.Git.Sha);
            _logger.LogInformation("Running in {EntryPoint}", KnownFolders.EntryPoint);

            SystemParameters p = _systemParams.Create();

            _logger.LogInformation("Detected Windows Version: {Version}", Environment.OSVersion.VersionString);

            _logger.LogInformation(
                "System settings - ({MemorySize} RAM) ({PageSize} Page), Display: {ScreenWidth} x {ScreenHeight} ({Vram} VRAM - VideoMemorySizeMb={ENBVRam})",
                p.SystemMemorySize.ToFileSizeString(), p.SystemPageSize.ToFileSizeString(), p.ScreenWidth, p.ScreenHeight,
                p.VideoMemorySize.ToFileSizeString(), p.EnbLEVRAMSize);

            if (p.SystemPageSize == 0)
                _logger.LogInformation(
                    "Page file is disabled! Consider increasing to 20000MB. A disabled page file can cause crashes and poor in-game performance");
            else if (p.SystemPageSize < 2e+10)
                _logger.LogInformation(
                    "Page file below recommended! Consider increasing to 20000MB. A suboptimal page file can cause crashes and poor in-game performance");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Main Window startup.");
            Environment.Exit(-1);
        }
    }
}

// Required so compiler doesn't complain about not finding the type. [MC3050]
public partial class Main { }
