using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack.UserIntervention;

public class ManualBrowserDownloadHandler : BrowserWindowViewModel
{
    public ManualBrowserDownload Intervention { get; set; }

    public ManualBrowserDownloadHandler(IServiceProvider serviceProvider, ILogger<ManualBrowserDownloadHandler> logger) : base(serviceProvider)
    {
        _logger = logger;
    }

    private ILogger<ManualBrowserDownloadHandler> _logger;

    protected override async Task Run(CancellationToken token)
    {
        var archive = Intervention.Archive;
        var md = Intervention.Archive.State as Manual;

        HeaderText = $"Manual download for {archive.Name} ({md.Url.Host})";

        Instructions = string.IsNullOrWhiteSpace(md.Prompt)
            ? $"Please download {archive.Name} with size {UIUtils.FormatBytes(archive.Size)}"
            : $"{md.Prompt} (file {archive.Name} with size {UIUtils.FormatBytes(archive.Size)})";

        bool success = false;
        try
        {
            success = await WaitForDownloadWithBrowser(md.Url, Intervention.Destination.ToString(), archive, token);
        }
        finally
        {
            Intervention.Finish(success);
        }
    }

    private async Task<bool> WaitForDownloadWithBrowser(Uri downloadUrl, string downloadPath, Archive archive, CancellationToken token)
    {
        await WaitForReady(token);

        while (!token.IsCancellationRequested)
        {
            var downloadCompleted = new TaskCompletionSource<bool>(token);
            var wrongDownload = new TaskCompletionSource<(string, long)>(token);

            EventHandler<CoreWebView2DownloadStartingEventArgs> handler = null!;

            handler = (sender, args) =>
            {
                if (IsCorrectFileDownloading(args.DownloadOperation, archive))
                {
                    args.ResultFilePath = downloadPath;
                    args.DownloadOperation.StateChanged += (o, o1) =>
                    {
                        var operation = (CoreWebView2DownloadOperation)o;
                        ProcessDownloadChange(operation, downloadCompleted);
                    };
                    ProcessDownloadChange(args.DownloadOperation, downloadCompleted);
                }
                else
                {
                    string resultFileName = Path.GetFileName(args.ResultFilePath);
                    args.Cancel = true;
                    wrongDownload.TrySetResult((resultFileName, (long)args.DownloadOperation.TotalBytesToReceive));
                }
                args.Handled = true;
                Browser.CoreWebView2.DownloadStarting -= handler;
            };

            try
            {
                Browser.CoreWebView2.DownloadStarting += handler;
                await NavigateTo(downloadUrl);
                await WaitWhileRemovingIframes(Task.WhenAny(downloadCompleted.Task, wrongDownload.Task), token);
            }
            finally
            {
                Browser.CoreWebView2.DownloadStarting -= handler;
            }

            if (downloadCompleted.Task.IsCompleted)
            {
                return downloadCompleted.Task.Result;
            }
            else
            {
                var (fileName, downloadSize) = wrongDownload.Task.Result;
                var result = Xceed.Wpf.Toolkit.MessageBox.Show(
                    $"Started download of file \"{fileName}\" with size {UIUtils.FormatBytes(downloadSize)}, " +
                    $"but expected \"{archive.Name}\" with size {UIUtils.FormatBytes(archive.Size)}.\n" +
                    $"Try again?",
                    "Trying to download wrong file",
                    MessageBoxButton.OKCancel);
                if (result != MessageBoxResult.OK)
                {
                    return false;
                }
            }
        }
        return false;
    }

    private void ProcessDownloadChange(CoreWebView2DownloadOperation downloadOperation, TaskCompletionSource<bool> downloadCompleted)
    {
        if (downloadOperation.State == CoreWebView2DownloadState.Completed)
        {
            downloadCompleted.TrySetResult(true);
        }
        if (downloadOperation.State == CoreWebView2DownloadState.Interrupted)
        {
            if (downloadOperation.CanResume)
            {
                downloadOperation.Resume();
            }
            else
            {
                _logger.LogWarning($"Failed to download file from {downloadOperation.Uri}: {downloadOperation.InterruptReason}");
                downloadCompleted.TrySetResult(false);
            }
        }
    }

    private bool IsCorrectFileDownloading(CoreWebView2DownloadOperation download, Archive archive)
    {
        ulong? bytesToReceive = download.TotalBytesToReceive;
        // if download size is different from expected archive size, it's surely wrong file
        if (bytesToReceive.HasValue && bytesToReceive != 0 && bytesToReceive != (ulong)archive.Size)
        {
            return false;
        }
        // consider more checks, by file name/extension for example
        return true;
    }
}