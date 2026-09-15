using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Networking.NexusApi;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     <paramref name="Credential" /> is what the probe found, whether or not the download path can use it, so
///     the check can say <em>how</em> the user is authenticated rather than a bare "Logged in as X", and can
///     explain a <c>NEXUS_API_KEY</c> that reads as logged out.
///     <para>
///         <see cref="HasToken" /> is derived from it rather than passed in: there is one definition of being
///         logged in to Nexus Mods, <see cref="NexusCredential.CanDownload" />, and it is the downloader's.
///         Nothing can report a login here that <c>NexusDownloader.Prepare</c> would refuse.
///         <paramref name="LoggedIn" /> false with a usable credential means the API rejected it - expired or
///         revoked; <paramref name="Error" /> says why.
///     </para>
/// </summary>
public record NexusLoginStatus(bool LoggedIn, bool IsPremium, string? UserName, string? Error,
    NexusCredentialSource Credential)
{
    /// <summary>True when there is a credential the download path can actually use.</summary>
    public bool HasToken => Credential.CanDownload();
}

/// <summary>
///     Seam over the Nexus API's validate call so checks can be tested without a network or a stored token.
/// </summary>
public interface INexusLoginProbe
{
    Task<NexusLoginStatus> Probe(CancellationToken token);
}
