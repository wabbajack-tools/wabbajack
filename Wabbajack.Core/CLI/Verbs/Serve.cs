using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.CLI.Console;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Server;

namespace Wabbajack.CLI.Verbs;

/// <summary>
/// CLI verb that starts an HTTP server for the Tauri desktop application.
/// </summary>
public class Serve
{
    private readonly ILogger<Serve> _logger;
    private readonly GameLocator _gameLocator;
    private readonly ApplicationInfo _appInfo;
    private readonly IConsoleRenderer _console;

    public Serve(ILogger<Serve> logger, GameLocator gameLocator, ApplicationInfo appInfo, IConsoleRenderer console)
    {
        _logger = logger;
        _gameLocator = gameLocator;
        _appInfo = appInfo;
        _console = console;
    }

    public static VerbDefinition Definition = new("serve",
        "Starts an HTTP server for the Wabbajack desktop application",
        new[]
        {
            new OptionDefinition(typeof(int), "p", "port", "Port to listen on (default: 13373)"),
            new OptionDefinition(typeof(bool), "o", "open", "Open browser automatically")
        });

    internal async Task<int> Run(int port, bool open, CancellationToken token)
    {
        // Default port if not specified
        if (port == 0)
            port = 13373;

        var eventBroadcaster = new EventBroadcaster(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EventBroadcaster>.Instance);

        var server = new HttpApiServer(port, _gameLocator, _appInfo, eventBroadcaster);

        _console.WriteMarkupLine($"[bold green]Wabbajack Server v{_appInfo.Version}[/]");
        _console.WriteMarkupLine($"[dim]Platform: {_appInfo.Platform}[/]");
        _console.WriteMarkupLine("");
        _console.WriteMarkupLine($"[bold]Listening on:[/] [link]http://localhost:{port}[/]");
        _console.WriteMarkupLine("");
        _console.WriteMarkupLine("[dim]Endpoints:[/]");
        _console.WriteMarkupLine($"  [blue]GET[/]  /api/hello?name=X  - Test greeting endpoint");
        _console.WriteMarkupLine($"  [blue]GET[/]  /api/games        - List installed games");
        _console.WriteMarkupLine($"  [blue]GET[/]  /api/status       - Server status");
        _console.WriteMarkupLine($"  [blue]GET[/]  /api/events       - SSE event stream");
        _console.WriteMarkupLine("");
        _console.WriteMarkupLine("[dim]Press Ctrl+C to stop the server[/]");

        if (open)
        {
            try
            {
                var url = $"http://localhost:{port}/api/hello?name=Wabbajack";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not open browser");
            }
        }

        // Start heartbeat task
        var heartbeatTask = eventBroadcaster.RunHeartbeatAsync(token);

        try
        {
            await server.RunAsync(token);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }

        _console.Info("Server stopped.");
        return 0;
    }
}
