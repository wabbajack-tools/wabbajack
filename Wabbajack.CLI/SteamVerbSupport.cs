using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Networking.Steam;

namespace Wabbajack.CLI;

/// <summary>
///     The bits every Steam content verb needs: getting logged in from the saved token, and turning the
///     failures Steam produces into something worth printing and an exit code.
/// </summary>
internal static class SteamVerbSupport
{
    /// <summary>
    ///     Logs in with the saved refresh token. Content verbs never prompt: authenticating is
    ///     <c>steam-login</c>'s job, and a verb that silently started a QR flow in the middle of a script
    ///     would be worse than one that says to go and run it.
    /// </summary>
    public static async Task EnsureLoggedInAsync(ISteamSession session, CancellationToken token)
    {
        if (session.IsLoggedIn) return;

        if (!session.HaveStoredToken)
            throw new SteamLoginRequiredException("There is no saved Steam login. Run steam-login first.");

        await session.LoginWithStoredTokenAsync(token);
    }

    /// <summary>
    ///     Takes the manifest id the user gave, or asks Steam what the depot is publishing right now when
    ///     they gave none. Only the current one can be asked for: Steam exposes no history, which is why
    ///     an older game version needs an id from Wabbajack's own index.
    /// </summary>
    public static async Task<ulong> ResolveManifestAsync(ISteamContentClient content, ILogger logger, uint app,
        uint depot, ulong manifest)
    {
        if (manifest != 0) return manifest;

        var current = await content.GetCurrentManifestIdAsync(app, depot)
                      ?? throw new SteamFileNotInDepotException(
                          $"Steam does not say what depot {depot} of app {app} is publishing on the public branch, " +
                          "so --manifest has to be given", string.Empty);

        logger.LogInformation("Depot {Depot} is publishing manifest {Manifest} on the public branch", depot,
            current);

        return current;
    }

    /// <summary>
    ///     Reports an exception from a content verb, or returns null when it is not one this knows about so
    ///     the caller can let it out.
    /// </summary>
    public static int? Report(ILogger logger, Exception ex)
    {
        switch (ex)
        {
            case OperationCanceledException:
                logger.LogInformation("Cancelled");
                return 1;

            case SteamNoEntitlementException entitlement:
                logger.LogError("{Message}", entitlement.Message);
                return 1;

            case SteamEntitlementUnconfirmedException unconfirmed:
                logger.LogError("{Message}", unconfirmed.Message);
                return 1;

            case SteamManifestUnavailableException manifest:
                logger.LogError("{Message}", manifest.Message);
                return 1;

            case SteamFileNotInDepotException notFound:
                logger.LogError("{Message}", notFound.Message);
                logger.LogInformation(
                    "steam-list-manifest will print what the manifest does contain");
                return 1;

            case SteamContentVerificationException verification:
                logger.LogError("{Message}", verification.Message);
                return 1;

            case SteamNoContentServersException servers:
                logger.LogError("{Message}", servers.Message);
                return 1;

            case SteamLoginRequiredException login:
                logger.LogError("{Message}", login.Message);
                return 1;

            case SteamLoginInProgressException inProgress:
                logger.LogError("{Message}", inProgress.Message);
                return 1;

            case SteamException steam:
                logger.LogError("Steam refused the request: {Result}", steam.Result);
                return 1;

            case TimeoutException:
                logger.LogError("Steam did not answer in time. Check your connection and try again");
                return 1;

            default:
                return null;
        }
    }
}
