using Microsoft.Extensions.Logging;
using SteamKit2;
using Wabbajack.Networking.Steam.DTOs;

namespace Wabbajack.Networking.Steam;

public class DepotDownloader
{
    private readonly ILogger<DepotDownloader> _logger;
    private readonly Client _steamClient;

    public DepotDownloader(ILogger<DepotDownloader> logger, Client client)
    {
        _logger = logger;
        _steamClient = client;
    }

    public async Task<bool> AccountHasAccess(uint depotId)
    {
        var packages = _steamClient.Licenses.Select(l => l.PackageID);
        var infos = await _steamClient.GetPackageInfos(packages);

        foreach (var info in infos.Where(i => i.Value != null))
        {
            if (info.Value!.KeyValues["appids"].Children.Any(child => child.AsUnsignedInteger() == depotId))
                return true;
            
            if (info.Value!.KeyValues["depotids"].Children.Any(child => child.AsUnsignedInteger() == depotId))
                return true;

        }
        return false;
    }

    public async Task<AppInfo> GetAppInfo(uint appId)
    {
        return await _steamClient.GetAppInfo(appId);
    }
}