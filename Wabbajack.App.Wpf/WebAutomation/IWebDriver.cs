using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.LibCefHelpers;

namespace Wabbajack.WebAutomation
{
    public interface IWebDriver
    {
        Task NavigateTo(Uri uri, CancellationToken? token = null);
        Task<string> EvaluateJavaScript(string text);
        Task<Helpers.Cookie[]> GetCookies(string domainPrefix);
        public Action<Uri>? DownloadHandler { get; set; } 
    }
}
