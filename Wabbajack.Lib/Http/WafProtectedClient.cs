using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.OffScreen;
using Wabbajack.Common;
using Wabbajack.Lib.LibCefHelpers;

namespace Wabbajack.Lib.Http
{
    /// <summary>
    /// Client for sites behind cloudflare WAF.
    /// </summary>
    public class WafProtectedClient : Client
    {
        private const string SuspendingWafPrefix = "www.";
        private static readonly TimeSpan s_passCloudflareTimeout = TimeSpan.FromSeconds(10);

        private readonly Uri _siteUrl;

        /// <param name="protectedPageUrl">Page that will be opened each time cloudflare protection was triggered.</param>
        public WafProtectedClient(Uri protectedPageUrl)
        {
            _siteUrl = StripUri(protectedPageUrl);
        }

        public static Uri StripUri(Uri uriToString)
        {
            if (!uriToString.IsAbsoluteUri)
            {
                throw new ArgumentException($"Site url should be absolute.", nameof(uriToString));
            }

            string host = uriToString.Host.StartsWith(SuspendingWafPrefix)
                ? uriToString.Host[SuspendingWafPrefix.Length..]
                : uriToString.Host;

            return new UriBuilder(uriToString.Scheme, host, uriToString.Port).Uri;
        }

        public static async Task<IEnumerable<Cookie>> GetFreshCookies(Uri url, CancellationToken cancellationToken)
        {
            TaskCompletionSource? passProtectionTaskSource = new();

            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(s_passCloudflareTimeout);
            _ = cts.Token.Register(() => passProtectionTaskSource.TrySetCanceled());

            var browser = new ChromiumWebBrowser(url.ToString());

            browser.LoadingStateChanged += HandleLoadingStateChanged;

            try
            {
                await passProtectionTaskSource.Task;
            }
            catch
            {
                var cookies = await Helpers.GetCookies(url.Host);
                Utils.Log("Failed to pass WAF protection.");
            }
            browser.LoadingStateChanged -= HandleLoadingStateChanged;
            return await browser.GetCookieManager().VisitAllCookiesAsync();

            void HandleLoadingStateChanged(object? sender, LoadingStateChangedEventArgs e)
            {
                _ = WaitCloudflareProtectedPassed(sender, e, passProtectionTaskSource);
            }
        }

        protected override async Task HandleWafProtection(HttpResponseMessage response, CancellationToken token)
        {
            if (IsProtectedByCloudflare(await response.Content.ReadAsStringAsync(token)))
            {
                var cookies = await GetFreshCookies(_siteUrl, token);

                foreach (var cookie in cookies)
                {
                    var clientCookie = new System.Net.Cookie
                    {
                        Name = cookie.Name,
                        Value = cookie.Value,
                        Domain = cookie.Domain,
                        Path = cookie.Path,
                        Secure = cookie.Secure,
                        HttpOnly = cookie.HttpOnly,
                        Expires = cookie.Expires ?? DateTime.Today.AddMonths(1)
                    };
                    Cookies.Add(clientCookie);
                    ClientFactory.Cookies.Add(clientCookie);
                }
            }
        }

        public static bool IsProtectedByCloudflare(string content) =>
            content.Contains("cf-browser-verification");

        private static async Task WaitCloudflareProtectedPassed(
            object? _,
            LoadingStateChangedEventArgs e,
            TaskCompletionSource tcs)
        {
            try
            {
                if (e.IsLoading)
                    return;

                string content = await e.Browser.MainFrame.GetSourceAsync();
                if (IsProtectedByCloudflare(content))
                    return;

                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }
    }
}
