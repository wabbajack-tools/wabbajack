using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders.ManualSources;

/// <summary>
///     Creation Club content has no page a user can download from; it is installed by the game itself.
///     The <c>.meta</c> and URL handling is kept so lists carrying these archives still compile and validate.
/// </summary>
public class BethesdaSource : AManualSourceDownloader<Bethesda>, IUrlDownloader
{
    protected override string Reason => "Creation Club content must be installed through the game";

    public override Task<Hash> Download(Archive archive, Bethesda state, AbsolutePath destination, IJob job,
        CancellationToken token)
    {
        throw new NotSupportedException("Creation Club content must be installed through the game");
    }

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return true;
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("directURL") && Uri.TryCreate(iniData["directURL"].CleanIniString(), UriKind.Absolute, out var uri))
        {
            return Parse(uri);
        }
        return null;
    }

    public override IEnumerable<string> MetaIni(Archive a, Bethesda state)
    {
        return new[] {$"directURL={UnParse(state)}"};
    }

    public IDownloadState? Parse(Uri uri)
    {
        if (uri.Scheme != "bethesda") return null;
        var path = uri.PathAndQuery.Split("/", StringSplitOptions.RemoveEmptyEntries);
        if (path.Length != 4) return null;
        var game = GameRegistry.TryGetByFuzzyName(uri.Host);
        if (game == null) return null;

        if (!long.TryParse(path[1], out var productId)) return null;
        if (!long.TryParse(path[2], out var branchId)) return null;

        bool isCCMod = false;
        switch (path[0])
        {
            case "cc":
                isCCMod = true;
                break;
            case "mod":
                isCCMod = false;
                break;
            default:
                return null;
        }

        return new Bethesda
        {
            Game = game.Game,
            IsCCMod = isCCMod,
            ProductId = productId,
            BranchId = branchId,
            ContentId = path[3]
        };
    }

    public Uri UnParse(IDownloadState state)
    {
        var cstate = (Bethesda) state;
        return new Uri($"bethesda://{cstate.Game}/{(cstate.IsCCMod ? "cc" : "mod")}/{cstate.ProductId}/{cstate.BranchId}/{cstate.ContentId}");
    }
}
