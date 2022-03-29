using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.Logins;

namespace Wabbajack.WebAutomation
{
    public interface IWebDriver
    {
        Task NavigateTo(Uri uri, CancellationToken? token = null);
        Task<string> EvaluateJavaScript(string text);
        Task<Cookie[]> GetCookies(string domainPrefix);
        public Action<Uri>? DownloadHandler { get; set; }
        public Task WaitForInitialized();
        ISchemeHandler WithSchemeHandler(Predicate<Uri> wabbajack);
    }

    public interface ISchemeHandler : IDisposable
    {
        public Task<Uri> Task { get; }
    }
}
