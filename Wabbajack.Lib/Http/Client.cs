using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Wabbajack.Common;
using Wabbajack.Common.Exceptions;
using Wabbajack.Lib.LibCefHelpers;

namespace Wabbajack.Lib.Http
{
    public class Client
    {
        public List<(string, string?)> Headers = new List<(string, string?)>();
        public List<Cookie> Cookies = new List<Cookie>();
        public async Task<HttpResponseMessage> GetAsync(string url, HttpCompletionOption responseHeadersRead = HttpCompletionOption.ResponseHeadersRead, bool errorsAsExceptions = true, bool retry = true, CancellationToken? token = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendAsync(request, responseHeadersRead, errorsAsExceptions: errorsAsExceptions, retry: retry, token: token);
        }
        
        public async Task<HttpResponseMessage> GetAsync(Uri url, HttpCompletionOption responseHeadersRead = HttpCompletionOption.ResponseHeadersRead, bool errorsAsExceptions = true, CancellationToken? token = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendAsync(request, responseHeadersRead, errorsAsExceptions: errorsAsExceptions, token:token);
        }
        
        
        public async Task<HttpResponseMessage> PostAsync(string url, HttpContent content, HttpCompletionOption responseHeadersRead = HttpCompletionOption.ResponseHeadersRead, bool errorsAsExceptions = true)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url) {Content = content};
            return await SendAsync(request, responseHeadersRead, errorsAsExceptions);
        }
        
        public async Task<HttpResponseMessage> PutAsync(string url, HttpContent content, HttpCompletionOption responseHeadersRead = HttpCompletionOption.ResponseHeadersRead)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, url) {Content = content};
            return await SendAsync(request, responseHeadersRead);
        }
        
        public async Task<string> GetStringAsync(string url, CancellationToken? token = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendStringAsync(request, token: token);
        }
        
        public async Task<string> GetStringAsync(Uri url, CancellationToken? token = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendStringAsync(request, token: token);
        }
        
        public async Task<string> DeleteStringAsync(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            return await SendStringAsync(request);
        }

        private async Task<string> SendStringAsync(HttpRequestMessage request, CancellationToken? token = null)
        {
            using var result = await SendAsync(request, token: token);
            if (!result.IsSuccessStatusCode)
            {
                Utils.Log("Internal Error");
                Utils.Log(await result.Content.ReadAsStringAsync());
                throw new Exception(
                    $"Bad HTTP request {result.StatusCode} {result.ReasonPhrase} - {request.RequestUri}");
            }
            return await result.Content.ReadAsStringAsync();
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage msg, HttpCompletionOption responseHeadersRead = HttpCompletionOption.ResponseHeadersRead, bool errorsAsExceptions = true, bool retry = true, CancellationToken? token = null)
        {
            foreach (var (k, v) in Headers) 
                msg.Headers.Add(k, v);
            if (Cookies.Count > 0)
                Cookies.ForEach(c => ClientFactory.Cookies.Add(c));   
            int retries = 0;
            HttpResponseMessage response;
            TOP:
            try
            {
                response = await ClientFactory.Client.SendAsync(msg, responseHeadersRead, token ?? CancellationToken.None);
                if (response.IsSuccessStatusCode) return response;

                if (errorsAsExceptions)
                {
                    response.Dispose();
                    throw new HttpException(response);
                }

                return response;
            }
            catch (Exception ex)
            {
                if (!retry) throw;
                if (ex is HttpException http)
                {
                    if (http.Code != 503 && http.Code != 521) throw;

                    retries++;
                    var ms = Utils.NextRandom(100, 1000);
                    Utils.Log($"Got a {http.Code} from {msg.RequestUri} retrying in {ms}ms");

                    await Task.Delay(ms, token ?? CancellationToken.None);
                    msg = CloneMessage(msg);
                    goto TOP;
                }
                if (retries > Consts.MaxHTTPRetries) throw;

                retries++;
                Utils.LogStraightToFile(ex.ToString());
                Utils.Log($"Http Connect error to {msg.RequestUri} retry {retries}");
                await Task.Delay(100 * retries, token ?? CancellationToken.None);
                msg = CloneMessage(msg);
                goto TOP;

            }

        }

        private HttpRequestMessage CloneMessage(HttpRequestMessage msg)
        {
            var new_message = new HttpRequestMessage(msg.Method, msg.RequestUri);
            foreach (var header in msg.Headers)
                new_message.Headers.Add(header.Key, header.Value);
            new_message.Content = msg.Content;
            return new_message;

        }

        public async Task<T> GetJsonAsync<T>(string s)
        {
            var result = await GetStringAsync(s);
            return result.FromJsonString<T>();
        }

        public async Task<HtmlDocument> GetHtmlAsync(string s, CancellationToken? token = null)
        {
            var body = await GetStringAsync(s, token: token);
            var doc = new HtmlDocument();
            doc.LoadHtml(body);
            return doc;
        }

        public Client WithHeader((string MetricsKeyHeader, string) header)
        {
            var newHeaders = Headers.Cons(header).ToList();
            var client = new Client {Headers = newHeaders, Cookies = Cookies,};
            return client;
        }

        public void AddCookies(Helpers.Cookie[] cookies)
        {
            Cookies.AddRange(cookies.Select(c => new Cookie {Domain = c.Domain, Name = c.Name, Value = c.Value, Path = c.Path}));
        }

        public void UseChromeUserAgent()
        {
            Headers.Add(("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/89.0.4389.82 Safari/537.36"));
        }

        public async Task DownloadAsync(Uri url, AbsolutePath path)
        {
            using var response = await GetAsync(url);
            await using var content = await response.Content.ReadAsStreamAsync();
            path.Parent.CreateDirectory();
            await using var of = await path.Create();
            await content.CopyToAsync(of);
        }
    }
}
