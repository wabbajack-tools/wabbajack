using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.API;
using Wabbajack.Installer;

namespace Wabbajack.Server;

/// <summary>
/// Singleton service that broadcasts events to connected SSE clients.
/// </summary>
public class EventBroadcaster
{
    private readonly ILogger<EventBroadcaster> _logger;
    private readonly ConcurrentDictionary<Guid, Channel<ServerEvent>> _clients = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public EventBroadcaster(ILogger<EventBroadcaster> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Registers a new SSE client and returns a channel for receiving events.
    /// </summary>
    public (Guid ClientId, ChannelReader<ServerEvent> Reader) RegisterClient()
    {
        var clientId = Guid.NewGuid();
        var channel = Channel.CreateBounded<ServerEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _clients[clientId] = channel;
        _logger.LogInformation("SSE client {ClientId} connected. Total clients: {Count}", clientId, _clients.Count);

        // Send connected event
        _ = channel.Writer.TryWrite(new ServerEvent(
            nameof(ServerEventType.Connected),
            DateTime.UtcNow,
            new { clientId = clientId.ToString() }));

        return (clientId, channel.Reader);
    }

    /// <summary>
    /// Unregisters an SSE client.
    /// </summary>
    public void UnregisterClient(Guid clientId)
    {
        if (_clients.TryRemove(clientId, out var channel))
        {
            channel.Writer.TryComplete();
            _logger.LogInformation("SSE client {ClientId} disconnected. Remaining clients: {Count}", clientId, _clients.Count);
        }
    }

    /// <summary>
    /// Broadcasts a status update event to all connected clients.
    /// </summary>
    public void BroadcastStatusUpdate(StatusUpdate update)
    {
        var evt = new ServerEvent(
            nameof(ServerEventType.StatusUpdate),
            DateTime.UtcNow,
            new
            {
                category = update.StatusCategory,
                text = update.StatusText,
                stepsProgress = update.StepsProgress.Value,
                stepProgress = update.StepProgress.Value,
                currentStep = update.CurrentStep
            });

        BroadcastEvent(evt);
    }

    /// <summary>
    /// Broadcasts a progress event to all connected clients.
    /// </summary>
    public void BroadcastProgress(string taskId, double progress, string? description = null)
    {
        var evt = new ServerEvent(
            nameof(ServerEventType.Progress),
            DateTime.UtcNow,
            new { taskId, progress, description });

        BroadcastEvent(evt);
    }

    /// <summary>
    /// Broadcasts an error event to all connected clients.
    /// </summary>
    public void BroadcastError(string message, string? details = null)
    {
        var evt = new ServerEvent(
            nameof(ServerEventType.Error),
            DateTime.UtcNow,
            new { message, details });

        BroadcastEvent(evt);
    }

    /// <summary>
    /// Sends a heartbeat to all connected clients.
    /// </summary>
    public void SendHeartbeat()
    {
        var evt = new ServerEvent(
            nameof(ServerEventType.Heartbeat),
            DateTime.UtcNow,
            null);

        BroadcastEvent(evt);
    }

    /// <summary>
    /// Broadcasts download progress for a modlist preparation operation.
    /// </summary>
    public void BroadcastDownloadProgress(string sessionId, double progress, long bytesDownloaded, long totalBytes)
    {
        var evt = new ServerEvent(
            "DownloadProgress",
            DateTime.UtcNow,
            new { sessionId, progress, bytesDownloaded, totalBytes });

        BroadcastEvent(evt);
    }

    /// <summary>
    /// Broadcasts checklist progress for a specific check type.
    /// </summary>
    public void BroadcastChecklistProgress(string sessionId, string checkType, double progress, string message)
    {
        var evt = new ServerEvent(
            "ChecklistProgress",
            DateTime.UtcNow,
            new { sessionId, checkType, progress, message });

        BroadcastEvent(evt);
    }

    /// <summary>
    /// Broadcasts per-file hash progress for large files.
    /// </summary>
    public void BroadcastHashProgress(string sessionId, string fileName, long bytesHashed, long totalBytes)
    {
        var evt = new ServerEvent(
            "HashProgress",
            DateTime.UtcNow,
            new { sessionId, fileName, bytesHashed, totalBytes, progress = totalBytes > 0 ? (double)bytesHashed / totalBytes : 0 });

        BroadcastEvent(evt);
    }

    private void BroadcastEvent(ServerEvent evt)
    {
        foreach (var (clientId, channel) in _clients)
        {
            if (!channel.Writer.TryWrite(evt))
            {
                _logger.LogWarning("Failed to write event to client {ClientId}, channel may be full", clientId);
            }
        }
    }

    /// <summary>
    /// Gets the number of connected clients.
    /// </summary>
    public int ConnectedClients => _clients.Count;

    /// <summary>
    /// Starts a background heartbeat task that sends periodic heartbeats to all clients.
    /// </summary>
    public async Task RunHeartbeatAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(cancellationToken);
                SendHeartbeat();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
