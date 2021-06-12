using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Lib.LibCefHelpers;

namespace Wabbajack.Lib.WebAutomation
{
    public interface IWebDriver
    {
        Task NavigateTo(Uri uri, CancellationToken? token = null);
        Task<string> EvaluateJavaScript(string text);
        Task<Helpers.Cookie[]> GetCookies(string domainPrefix);
        public Action<Uri>? DownloadHandler { get; set; } 
    }
}
