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

    public static HttpRequestMessage ToHttpRequestMessage(this ManualDownload.BrowserDownloadState browserState)
    {
        var msg = new HttpRequestMessage(HttpMethod.Get, browserState.Uri);
        msg.Headers.Add("User-Agent", browserState.UserAgent);
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