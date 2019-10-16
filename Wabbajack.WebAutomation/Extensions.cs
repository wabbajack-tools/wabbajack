using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;
using Wabbajack.Common;
using Cookie = System.Net.Cookie;

namespace Wabbajack.WebAutomation
{
    public static class Extensions
    {
        public static HttpClient ConvertToHTTPClient(this IWebDriver driver)
        {
            var user_agent = ((IJavaScriptExecutor) driver).ExecuteScript("return navigator.userAgent");

            var cookies = driver.Manage().Cookies;

            var container = new CookieContainer();
            foreach (var cookie in cookies.AllCookies)
            {
                var uri = new UriBuilder(new Uri(driver.Url));
                container.Add(uri.Uri, new Cookie(cookie.Name, cookie.Value));
            }

            var handler = new HttpClientHandler() {CookieContainer = container};
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", (string)user_agent);

            return client;
        }

        public static bool DownloadUrl(this HttpClient client, string url, string dest, bool download=true)
        {
            long total_read = 0;
            var buffer_size = 1024 * 32;

            var response = client.GetSync(url);
            var stream = response.Content.ReadAsStreamAsync();
            try
            {
                stream.Wait();
            }
            catch (Exception)
            {
            }

            if (stream.IsFaulted)
            {
                Utils.Log($"While downloading {url} - {stream.Exception.ExceptionToString()}");
                return false;
            }

            if (!download)
                return true;

            var header_var = "1";
            if (response.Content.Headers.Contains("Content-Length"))
                header_var = response.Content.Headers.GetValues("Content-Length").FirstOrDefault();

            var content_size = header_var != null ? long.Parse(header_var) : 1;

            var filename = Path.GetFileName(dest);
            using (var webs = stream.Result)
            using (var fs = File.OpenWrite(dest))
            {
                var buffer = new byte[buffer_size];
                while (true)
                {
                    var read = webs.Read(buffer, 0, buffer_size);
                    if (read == 0) break;
                    Utils.Status( $"Downloading {filename}", (int)(total_read * 100 / content_size));

                    fs.Write(buffer, 0, read);
                    total_read += read;
                }
            }

            return true;
        }
    }
}
