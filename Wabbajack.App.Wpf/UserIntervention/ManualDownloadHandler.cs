using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Paths;

namespace Wabbajack.UserIntervention;

public class ManualDownloadHandler : BrowserTabViewModel
{
    public ManualDownload Intervention { get; set; }

    protected override async Task Run(CancellationToken token)
    {
        await WaitForReady();
        var archive = Intervention.Archive;
        var md = Intervention.Archive.State as Manual;
        
        HeaderText = $"Manual download ({md.Url.Host})";
        
        Instructions = string.IsNullOrWhiteSpace(md.Prompt) ? $"Please download {archive.Name}" : md.Prompt;
        await NavigateTo(md.Url);

        var uri = await WaitForDownloadUri(token);
        
        Intervention.Finish(uri);
    }
}