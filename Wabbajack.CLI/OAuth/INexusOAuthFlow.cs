using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.CLI.OAuth;

public interface INexusOAuthFlow
{
    Task<string?> GetAuthorizationCode(Uri authorizeUrl, string expectedState, CancellationToken token);
}
