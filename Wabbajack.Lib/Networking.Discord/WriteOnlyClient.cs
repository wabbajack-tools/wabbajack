using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Discord;

public class WriteOnlyClient
{
    private readonly HttpClient _httpClient;
    private readonly IResource<HttpClient> _limiter;
    private readonly ILogger<WriteOnlyClient> _logger;
    private readonly ITokenProvider<Dictionary<Channel, DiscordWebHookSetting>> _token;

    public WriteOnlyClient(ILogger<WriteOnlyClient> logger,
        ITokenProvider<Dictionary<Channel, DiscordWebHookSetting>> token, HttpClient client,
        IResource<HttpClient> limiter)
    {
        _logger = logger;
        _token = token;
        _httpClient = client;
        _limiter = limiter;
    }

    public async Task SendAsync(Channel channel, DiscordMessage message, CancellationToken token)
    {
        try
        {
            var setting = (await _token.Get())![channel];
            var content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json");

            using var job = await _limiter.Begin($"Sending Discord Message to {channel}", 0, token);

            var msg = new HttpRequestMessage(HttpMethod.Post, setting.WebHook);
            msg.Content = content;

            await _httpClient.SendAsync(msg, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While sending message to {Channel}", channel);
        }
    }

    public async Task SendAsync(Channel channel, string message, CancellationToken token)
    {
        await SendAsync(channel, new DiscordMessage {Content = message}, token);
    }
}