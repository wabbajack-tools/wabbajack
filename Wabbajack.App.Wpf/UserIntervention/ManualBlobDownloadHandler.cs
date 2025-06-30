using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.UserIntervention;

public class ManualBlobDownloadHandler : BrowserWindowViewModel
{
    public ManualBlobDownload Intervention { get; set; }

    public ManualBlobDownloadHandler(IServiceProvider serviceProvider) : base(serviceProvider) { }

    protected override async Task Run(CancellationToken token)
    {
        var archive = Intervention.Archive;
        var md = Intervention.Archive.State as Manual;
        
        HeaderText = $"Manual download ({md.Url.Host})";

        Instructions = string.IsNullOrWhiteSpace(md.Prompt) ? $"Please download {archive.Name}" : md.Prompt;
        Hash hash = default;
        try
        {
            var tsk = WaitForDownload(Intervention.Destination, token);
            await NavigateTo(md.Url);
            hash = await tsk;
        }
        finally
        {
            Intervention.Finish(hash);
        }
    }
}