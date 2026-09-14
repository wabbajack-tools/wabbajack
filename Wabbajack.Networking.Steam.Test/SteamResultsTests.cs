using SteamKit2;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

public class SteamResultsTests
{
    [Theory]
    // Steam's answer for an expired or revoked refresh token. A token login never sends a password, so
    // reporting this as a password problem sends people off resetting a password that was always fine.
    [InlineData(EResult.InvalidPassword)]
    [InlineData(EResult.InvalidSignature)]
    [InlineData(EResult.AccessDenied)]
    [InlineData(EResult.Expired)]
    [InlineData(EResult.Revoked)]
    public void ADeadStoredCredentialIsRecognised(EResult result)
    {
        Assert.True(SteamResults.IsDeadCredential(result));
    }

    [Theory]
    [InlineData(EResult.OK)]
    [InlineData(EResult.NoConnection)]
    [InlineData(EResult.ServiceUnavailable)]
    [InlineData(EResult.Timeout)]
    [InlineData(EResult.TryAnotherCM)]
    [InlineData(EResult.RateLimitExceeded)]
    [InlineData(EResult.AccountLoginDeniedNeedTwoFactor)]
    [InlineData(EResult.AccountLogonDenied)]
    [InlineData(EResult.AccountDisabled)]
    [InlineData(EResult.Fail)]
    public void EverythingElseIsNotADeadCredential(EResult result)
    {
        Assert.False(SteamResults.IsDeadCredential(result));
    }
}
