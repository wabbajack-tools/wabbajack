using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack.UserIntervention;

public class ManualDownloadHandler : BrowserWindowViewModel
{
    public ManualDownload Intervention { get; set; }

    protected override async Task Run(CancellationToken token)
    {
        //await WaitForReady();
        var archive = Intervention.Archive;
        var md = Intervention.Archive.State as Manual;

        HeaderText = $"Manual download ({md.Url.Host})";

        Instructions = string.IsNullOrWhiteSpace(md.Prompt) ? $"Please download {archive.Name}" : md.Prompt;

        var task = WaitForDownloadUri(token, async () =>
        {
            await RunJavaScript("Array.from(document.getElementsByTagName(\"iframe\")).forEach(f => {if (f.title != \"SP Consent Message\" && !f.src.includes(\"challenges.cloudflare.com\")) f.remove()})");
        });
        await NavigateTo(md.Url);
        var uri = await task;

        Intervention.Finish(uri);
        await Task.Delay(5000, CancellationToken.None);
    }
}