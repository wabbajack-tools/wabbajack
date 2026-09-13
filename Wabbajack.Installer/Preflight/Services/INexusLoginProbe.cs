using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Installer.Preflight;

/// <summary>
///     <paramref name="HasToken" /> false: nothing stored, the user never logged in. <paramref name="LoggedIn" />
///     false with a token: the token no longer validates (expired or revoked); <paramref name="Error" /> says why.
/// </summary>
public record NexusLoginStatus(bool HasToken, bool LoggedIn, bool IsPremium, string? UserName, string? Error);

/// <summary>
///     Seam over the Nexus API's validate call so checks can be tested without a network or a stored token.
/// </summary>
public interface INexusLoginProbe
{
    Task<NexusLoginStatus> Probe(CancellationToken token);
}
