using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.API;
using Wabbajack.Paths.IO;

namespace Wabbajack.Server;

/// <summary>
/// Simple HTTP server using HttpListener for the REST API.
/// </summary>
public class HttpApiServer
{
    private readonly GameLocator _gameLocator;
    private readonly ApplicationInfo _appInfo;
    private readonly EventBroadcaster _eventBroadcaster;
    private readonly ModlistPreparer _modlistPreparer;
    private readonly HttpListener _listener;
    private readonly JsonSerializerOptions _jsonOptions;

    public HttpApiServer(
        int port,
        GameLocator gameLocator,
        ApplicationInfo appInfo,
        EventBroadcaster eventBroadcaster,
        ModlistPreparer modlistPreparer)
    {
        _gameLocator = gameLocator;
        _appInfo = appInfo;
        _eventBroadcaster = eventBroadcaster;
        _modlistPreparer = modlistPreparer;

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _listener.Start();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var contextTask = _listener.GetContextAsync();
                using var registration = cancellationToken.Register(() => _listener.Stop());

                HttpListenerContext context;
                try
                {
                    context = await contextTask;
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // Handle request in background
                _ = HandleRequestAsync(context, cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                // Continue listening on errors
            }
        }

        _listener.Stop();
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            // Add CORS headers
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            // Handle preflight
            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            var path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "/";

            var handled = path switch
            {
                "/api/hello" => await HandleHelloAsync(context),
                "/api/games" => await HandleGamesAsync(context),
                "/api/status" => await HandleStatusAsync(context),
                "/api/events" => await HandleEventsAsync(context, cancellationToken),
                "/api/modlist/prepare" when context.Request.HttpMethod == "POST"
                    => await HandleModlistPrepareAsync(context, cancellationToken),
                var p when p.StartsWith("/api/modlist/") && p.EndsWith("/status")
                    => await HandleModlistStatusAsync(context, p),
                var p when p.StartsWith("/api/modlist/") && p.EndsWith("/info")
                    => await HandleModlistInfoAsync(context, p),
                _ => false
            };

            if (!handled)
            {
                context.Response.StatusCode = 404;
                await WriteJsonAsync(context, ApiResponse<object>.Fail("Endpoint not found"));
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            try
            {
                await WriteJsonAsync(context, ApiResponse<object>.Fail(ex.Message));
            }
            catch
            {
                // Ignore write errors
            }
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }
    }

    private async Task<bool> HandleHelloAsync(HttpListenerContext context)
    {
        var query = context.Request.QueryString;
        var name = query["name"];

        var greeting = string.IsNullOrWhiteSpace(name)
            ? "Hello, World!"
            : $"Hello, {name}!";

        var response = ApiResponse<HelloResponse>.Ok(new HelloResponse(
            greeting,
            _appInfo.Version,
            DateTime.UtcNow));

        await WriteJsonAsync(context, response);
        return true;
    }

    private async Task<bool> HandleGamesAsync(HttpListenerContext context)
    {
        var games = new List<GameInfo>();

        foreach (var game in GameRegistry.Games.OrderBy(g => g.Value.HumanFriendlyGameName))
        {
            if (_gameLocator.IsInstalled(game.Key))
            {
                var location = _gameLocator.GameLocation(game.Key);
                var mainFile = game.Value.MainExecutable?.RelativeTo(location);

                string version = "Unknown";
                if (mainFile.HasValue && mainFile.Value.FileExists())
                {
                    try
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(mainFile.Value.ToString());
                        version = versionInfo.ProductVersion ?? versionInfo.FileVersion ?? "Unknown";
                    }
                    catch
                    {
                        // Ignore version read errors
                    }
                }

                games.Add(new GameInfo(
                    game.Value.HumanFriendlyGameName,
                    game.Key.ToString(),
                    true,
                    version,
                    location.ToString()));
            }
            else
            {
                games.Add(new GameInfo(
                    game.Value.HumanFriendlyGameName,
                    game.Key.ToString(),
                    false,
                    "-",
                    "-"));
            }
        }

        await WriteJsonAsync(context, ApiResponse<List<GameInfo>>.Ok(games));
        return true;
    }

    private async Task<bool> HandleStatusAsync(HttpListenerContext context)
    {
        var status = new
        {
            status = "running",
            version = _appInfo.Version,
            platform = _appInfo.Platform,
            connectedClients = _eventBroadcaster.ConnectedClients,
            timestamp = DateTime.UtcNow
        };

        await WriteJsonAsync(context, ApiResponse<object>.Ok(status));
        return true;
    }

    private async Task<bool> HandleEventsAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Add("Cache-Control", "no-cache");
        context.Response.Headers.Add("Connection", "keep-alive");

        var (clientId, reader) = _eventBroadcaster.RegisterClient();

        try
        {
            await using var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, leaveOpen: true);

            await foreach (var evt in reader.ReadAllAsync(cancellationToken))
            {
                var json = JsonSerializer.Serialize(evt, _jsonOptions);
                await writer.WriteAsync($"data: {json}\n\n");
                await writer.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal disconnection
        }
        finally
        {
            _eventBroadcaster.UnregisterClient(clientId);
        }

        return true;
    }

    private async Task<bool> HandleModlistPrepareAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var request = JsonSerializer.Deserialize<ModlistPrepareRequest>(body, _jsonOptions);
        if (request == null || string.IsNullOrEmpty(request.DownloadUrl))
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context, ApiResponse<ModlistPrepareStatus>.Fail("Missing downloadUrl in request"));
            return true;
        }

        var status = await _modlistPreparer.PrepareAsync(request, cancellationToken);
        await WriteJsonAsync(context, ApiResponse<ModlistPrepareStatus>.Ok(status));
        return true;
    }

    private async Task<bool> HandleModlistStatusAsync(HttpListenerContext context, string path)
    {
        // Extract session ID from path: /api/modlist/{sessionId}/status
        var segments = path.Split('/');
        if (segments.Length < 4)
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context, ApiResponse<ModlistPrepareStatus>.Fail("Invalid path"));
            return true;
        }

        var sessionId = segments[3];
        var status = _modlistPreparer.GetStatus(sessionId);

        if (status == null)
        {
            // Check if it's in the cache (already completed)
            var prepared = _modlistPreparer.GetPrepared(sessionId);
            if (prepared != null)
            {
                status = new ModlistPrepareStatus(sessionId, "ready", 1.0, null);
            }
            else
            {
                context.Response.StatusCode = 404;
                await WriteJsonAsync(context, ApiResponse<ModlistPrepareStatus>.Fail("Session not found"));
                return true;
            }
        }

        await WriteJsonAsync(context, ApiResponse<ModlistPrepareStatus>.Ok(status));
        return true;
    }

    private async Task<bool> HandleModlistInfoAsync(HttpListenerContext context, string path)
    {
        // Extract session ID from path: /api/modlist/{sessionId}/info
        var segments = path.Split('/');
        if (segments.Length < 4)
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context, ApiResponse<ModlistPreInstallInfo>.Fail("Invalid path"));
            return true;
        }

        var sessionId = segments[3];
        var prepared = _modlistPreparer.GetPrepared(sessionId);

        if (prepared == null)
        {
            context.Response.StatusCode = 404;
            await WriteJsonAsync(context, ApiResponse<ModlistPreInstallInfo>.Fail("Modlist not prepared or not found"));
            return true;
        }

        await WriteJsonAsync(context, ApiResponse<ModlistPreInstallInfo>.Ok(prepared.Info));
        return true;
    }

    private async Task WriteJsonAsync<T>(HttpListenerContext context, T data)
    {
        context.Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await context.Response.OutputStream.WriteAsync(bytes);
    }
}
