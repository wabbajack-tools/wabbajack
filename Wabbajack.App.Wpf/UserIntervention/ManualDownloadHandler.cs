using Microsoft.Web.WebView2.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack;

public class ManualDownloadHandler : BrowserWindowViewModel
{
    public ManualDownload Intervention { get; set; }

    public ManualDownloadHandler(IServiceProvider serviceProvider) : base(serviceProvider) { }

    protected override async Task Run(CancellationToken token)
    {
        var dowloadState = default(ManualDownload.BrowserDownloadState);
        try
        {
            var archive = Intervention.Archive;
            var md = Intervention.Archive.State as Manual;

            HeaderText = $"Manual download for {archive.Name} ({md.Url.Host})";

            Instructions = string.IsNullOrWhiteSpace(md.Prompt) ? $"Please download {archive.Name}" : md.Prompt;

            dowloadState = await NavigateAndLoadDownloadState(md.Url, token);
        }
        finally
        {
            Intervention.Finish(dowloadState);
        }
    }

    private async Task<ManualDownload.BrowserDownloadState> NavigateAndLoadDownloadState(Uri downloadPageUrl, CancellationToken token)
    {
        var source = new TaskCompletionSource<Uri>();
        var referer = Browser.Source;
        await WaitForReady(token);

        EventHandler<CoreWebView2DownloadStartingEventArgs> handler = null!;

        handler = (_, args) =>
        {
            try
            {
                source.TrySetResult(new Uri(args.DownloadOperation.Uri));
            }
            catch (Exception)
            {
                source.TrySetCanceled(token);
            }

            args.Cancel = true;
            args.Handled = true;
        };

        Browser.CoreWebView2.DownloadStarting += handler;

        try
        {
            var uri = await base.WaitWhileRemovingIframes(source.Task, token);

            var cookies = await GetCookies(uri.Host, token);

            return new ManualDownload.BrowserDownloadState(
                uri,
                cookies,
                new[]
                {
                ("Referer", referer?.ToString() ?? uri.ToString())
                },
                Browser.CoreWebView2.Settings.UserAgent);
        }
        finally
        {
            Browser.CoreWebView2.DownloadStarting -= handler;
        }

    }
}