using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Wabbajack.Common.Http
{
    public class Client
    {
        public List<(string, string)> Headers = new List<(string, string)>();
        public List<Cookie> Cookies = new List<Cookie>();
        public async Task<HttpResponseMessage> GetAsync(string url, HttpCompletionOption responseHeadersRead = HttpCompletionOption.ResponseHeadersRead)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var (k, v) in Headers) 
                request.Headers.Add(k, v);
            return await SendAsync(request, responseHeadersRead);
        }
        
        
        public async Task<HttpResponseMessage> PostAsync(string url, HttpContent content, HttpCompletionOption responseHeadersRead = HttpCompletionOption.ResponseHeadersRead)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url) {Content = content};
            foreach (var (k, v) in Headers) 
                request.Headers.Add(k, v);
            return await SendAsync(request, responseHeadersRead);
        }
        
        public async Task<HttpResponseMessage> PutAsync(string url, HttpContent content, HttpCompletionOption responseHeadersRead = HttpCompletionOption.ResponseHeadersRead)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, url) {Content = content};
            foreach (var (k, v) in Headers) 
                request.Headers.Add(k, v);
            return await SendAsync(request, responseHeadersRead);
        }
        
        public async Task<string> GetStringAsync(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var (k, v) in Headers) 
                request.Headers.Add(k, v);
            if (Cookies.Count > 0)
                Cookies.ForEach(c => ClientFactory.Cookies.Add(c));
                
            return await SendStringAsync(request);
        }
        
        public async Task<Stream> GetStreamAsync(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var (k, v) in Headers) 
                request.Headers.Add(k, v);
            if (Cookies.Count > 0)
                Cookies.ForEach(c => ClientFactory.Cookies.Add(c));
                
            return await SendStreamAsync(request);
        }

        private async Task<string> SendStringAsync(HttpRequestMessage request)
        {
            var result = await SendAsync(request);
            return await result.Content.ReadAsStringAsync();
        }
        
        private async Task<Stream> SendStreamAsync(HttpRequestMessage request)
        {
            var result = await SendAsync(request);
            return await result.Content.ReadAsStreamAsync();
        }


        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage msg, HttpCompletionOption responseHeadersRead = HttpCompletionOption.ResponseHeadersRead)
        {
            int retries = 0;
            TOP:
            try
            {
                var response = await ClientFactory.Client.SendAsync(msg, responseHeadersRead);
                return response;
            }
            catch (Exception)
            {
                if (retries > Consts.MaxHTTPRetries) throw;

                retries++;
                Utils.Log($"Http Connect error to {msg.RequestUri} retry {retries}");
                await Task.Delay(100 * retries);
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
    }
}
