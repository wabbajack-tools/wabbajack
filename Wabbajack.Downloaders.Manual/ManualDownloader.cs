using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.Manual;

public class ManualDownloader : ADownloader<DTOs.DownloadStates.Manual>
{
    public override Task<Hash> Download(Archive archive, DTOs.DownloadStates.Manual state, AbsolutePath destination, IJob job, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public override async Task<bool> Prepare()
    {
        return true;
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return allowList.AllowedPrefixes.Any(p => ((DTOs.DownloadStates.Manual) state).Url.ToString().StartsWith(p));
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("manualURL") && Uri.TryCreate(iniData["manualURL"], UriKind.Absolute, out var uri))
        {
            iniData.TryGetValue("prompt", out var prompt);
            
            var state = new DTOs.DownloadStates.Manual
            {
                Url = uri,
                Prompt = prompt ?? ""
            };

            return state;
        }

        return null;
    }

    public override Priority Priority { get; } = Priority.Lowest;
    public override async Task<bool> Verify(Archive archive, DTOs.DownloadStates.Manual archiveState, IJob job, CancellationToken token)
    {
        return true;
    }

    public override IEnumerable<string> MetaIni(Archive a, DTOs.DownloadStates.Manual state)
    {

        return new[] {$"manualURL={state.Url}", $"prompt={state.Prompt}"};
    }
}