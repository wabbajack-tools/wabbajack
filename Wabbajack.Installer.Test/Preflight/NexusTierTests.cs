#nullable enable
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Networking.NexusApi;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class NexusTierTests
{
    private static readonly NexusCredentialSource Usable = NexusCredentialSource.OAuth;

    public static TheoryData<NexusLoginStatus?, string> Statuses => new()
    {
        {new NexusLoginStatus(true, true, "someone", null, Usable), "premium"},
        {new NexusLoginStatus(true, false, "someone", null, Usable), "free"},
        {new NexusLoginStatus(false, false, null, null, NexusCredentialSource.None), "logged_out"},
        // The environment variable drives the API but is not a login, so it reads the way the checklist reads it.
        {new NexusLoginStatus(false, false, null, null, NexusCredentialSource.EnvironmentApiKey), "logged_out"},
        // There is a credential and the API would not answer for it: expired, or the network went away.
        {new NexusLoginStatus(false, false, null, "refused", Usable), "unknown"},
        {null, "unknown"}
    };

    [Theory]
    [MemberData(nameof(Statuses))]
    public void NamesTheTierOfALogin(NexusLoginStatus? status, string expected)
    {
        Assert.Equal(expected, NexusTier.Of(status));
    }

    [Theory]
    [InlineData(null, false, null)]
    [InlineData(null, true, null)]
    [InlineData(false, false, null)]
    [InlineData(true, true, null)]
    [InlineData(false, true, "free_to_premium")]
    [InlineData(true, false, "premium_to_free")]
    public void AChangeNeedsTwoDifferentAnswers(bool? wasPremium, bool isPremium, string? expected)
    {
        Assert.Equal(expected, NexusTier.Change(wasPremium, isPremium));
    }
}
