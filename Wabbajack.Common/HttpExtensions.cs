using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http;
using Wabbajack.RateLimiter;

namespace Wabbajack.Common;

public static class HttpExtensions
{
    public static HttpRequestMessage AddCookies(this HttpRequestMessage msg, Cookie[] cookies)
    {
        msg.Headers.Add("Cookie", string.Join(";", cookies.Select(c => $"{c.Name}={c.Value}")));
        return msg;
    }
    
    public static HttpRequestMessage AddHeaders(this HttpRequestMessage msg, IEnumerable<(string Key, string Value)> headers)
    {
        foreach (var header in headers)
        {
            msg.Headers.Add(header.Key, header.Value);
        }
        return msg;
    }

    public static HttpRequestMessage AddChromeAgent(this HttpRequestMessage msg)
    {
        msg.Headers.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36");
        return msg;
    }

    public static HttpRequestMessage ToHttpRequestMessage(this ManualDownload.BrowserDownloadState browserState)
    {
        var msg = new HttpRequestMessage(HttpMethod.Get, browserState.Uri);
        msg.AddChromeAgent();
        msg.AddCookies(browserState.Cookies);
        msg.AddHeaders(browserState.Headers);
        return msg;
    }

    public static async Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, IResource<HttpClient> limiter,
        HttpRequestMessage msg,
        JsonSerializerOptions? options, CancellationToken cancellationToken = default)
    {
        using var job = await limiter.Begin($"HTTP Get JSON {msg.RequestUri}", 0, cancellationToken);
        using var response = await client.SendAsync(msg, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpException(response);

        await job.Report((int) response.Content.Headers.ContentLength!, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TValue>(options, cancellationToken);
    }
}