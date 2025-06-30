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
        var uri = default(ManualDownload.BrowserDownloadState);
        try
        {
            var archive = Intervention.Archive;
            var md = Intervention.Archive.State as Manual;

            HeaderText = $"Manual download for {archive.Name} ({md.Url.Host})";

            Instructions = string.IsNullOrWhiteSpace(md.Prompt) ? $"Please download {archive.Name}" : md.Prompt;

            var task = WaitForDownloadUri(token, async () =>
            {
                await RunJavaScript("Array.from(document.getElementsByTagName(\"iframe\")).forEach(f => {if (f.title != \"SP Consent Message\" && !f.src.includes(\"challenges.cloudflare.com\")) f.remove()})");
            });
            await NavigateTo(md.Url);
            uri = await task;
        }
        finally
        {
            Intervention.Finish(uri);
        }
    }
}