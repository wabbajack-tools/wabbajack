using System;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Wabbajack.DTOs.Logins;

namespace Wabbajack.Abstractions;

// The browser operations the login/OAuth/manual-download handlers need, decoupled from any
// concrete WebView control. The Avalonia head implements this over WebView2.Avalonia's CoreWebView2
// (proven in the Phase 0 spike): navigation, cookie capture, request-header injection, JS exec, DOM.
public interface IWabbajackWebView
{
    Task WaitForReady(CancellationToken token = default);
    Task NavigateTo(Uri uri, CancellationToken token = default);
    Task<Cookie[]> GetCookies(string domainEnding, CancellationToken token = default);
    Task RunJavaScript(string script);
    Task<string> EvaluateJavaScript(string js);
    Task<HtmlDocument> GetDom(CancellationToken token = default);
    Task<T> WaitWhileRemovingIframes<T>(Task<T> mainTask, CancellationToken token);
    void GoBack();
}
