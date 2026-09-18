using System;
using System.Linq;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     The one thing a game file repair does to the user's Steam account rather than their disk. Both
///     directions matter: saying nothing when a free licence is about to be taken hides it, and saying it on
///     every repair trains people to scroll past the one time it is true.
/// </summary>
public class FreeLicenseAppsTests
{
    private const uint SkyrimSECreationKit = 1946180;

    [Fact]
    public void ARepairThatReachesNoCompanionAppSaysNothing()
    {
        Assert.Empty(FreeLicenseApps.Describe(Array.Empty<string>()));
    }

    [Fact]
    public void ANamedAppIsCalledWhatItIs()
    {
        Assert.Equal("the Skyrim Special Edition Creation Kit",
            FreeLicenseApps.Name(SkyrimSECreationKit, "Skyrim Special Edition"));
    }

    /// <summary>
    ///     An app added to a game's <c>SteamToolIDs</c> and not to the name table is still named, because
    ///     the point of the sentence is to say what is about to be added to the account.
    /// </summary>
    [Fact]
    public void AnUnnamedAppIsStillNamedByItsGameAndNumber()
    {
        var name = FreeLicenseApps.Name(4242, "Fallout 76");

        Assert.Contains("Fallout 76", name);
        Assert.Contains("4242", name);
    }

    /// <summary>
    ///     The wording may not promise the app <em>will</em> be added: whether a companion app is reached is
    ///     not known until the game's own depots have been searched, which is after the licence would have
    ///     been taken.
    /// </summary>
    [Fact]
    public void TheSentenceIsConditionalAndSaysWhatItCosts()
    {
        var said = Assert.Single(FreeLicenseApps.Describe(new[] {"the Skyrim Special Edition Creation Kit"}));

        Assert.Contains("If one of them turns out to be needed", said);
        Assert.Contains("Steam library", said);
        Assert.Contains("Nothing is bought", said);
        Assert.Contains("the Skyrim Special Edition Creation Kit", said);
    }

    /// <summary>One sentence however many apps are in reach, and each named once.</summary>
    [Fact]
    public void TheSameAppTwiceIsSaidOnce()
    {
        var said = Assert.Single(FreeLicenseApps.Describe(new[]
            {"the Fallout 4 Creation Kit", "the Fallout 4 Creation Kit"}));

        Assert.Equal(1, said.Split("the Fallout 4 Creation Kit").Length - 1);
    }

    [Fact]
    public void TwoAppsAreJoinedIntoTheOneSentence()
    {
        var said = Assert.Single(FreeLicenseApps.Describe(new[]
            {"the Skyrim Creation Kit", "the Fallout 4 Creation Kit"}));

        Assert.Contains("the Skyrim Creation Kit and the Fallout 4 Creation Kit", said);
    }
}
