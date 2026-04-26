using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Networking.Http;

public static class Extensions
{
    public static async Task<T> GetJsonFromSendAsync<T>(this HttpClient client, HttpRequestMessage msg,
        JsonSerializerOptions opts, CancellationToken? token = null)
    {
        token ??= CancellationToken.None;
        using var result = await client.SendAsync(msg, token.Value);
        if (!result.IsSuccessStatusCode)
        {
            throw new HttpException(result);
        }
        return (await JsonSerializer.DeserializeAsync<T>(await result.Content.ReadAsStreamAsync(token.Value)))!;
    }

    public static WebHeaderCollection ToWebHeaderCollection(this HttpRequestHeaders headers)
    {
        var headerCollection = new WebHeaderCollection();

        foreach (var header in headers.Where(header => !WebHeaderCollection.IsRestricted(header.Key)))
        {
            header.Value.ToList().ForEach(value => headerCollection.Add(header.Key, value));
        }

        return headerCollection;
    }
}