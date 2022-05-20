using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack.UserIntervention;

public class ManualBlobDownloadHandler : BrowserWindowViewModel
{
    public ManualBlobDownload Intervention { get; set; }

    protected override async Task Run(CancellationToken token)
    {
        await WaitForReady();
        var archive = Intervention.Archive;
        var md = Intervention.Archive.State as Manual;
        
        HeaderText = $"Manual download ({md.Url.Host})";

        Instructions = string.IsNullOrWhiteSpace(md.Prompt) ? $"Please download {archive.Name}" : md.Prompt;
        var tsk = WaitForDownload(Intervention.Destination, token);
        await NavigateTo(md.Url);
        var hash = await tsk;
        
        Intervention.Finish(hash);
    }
}