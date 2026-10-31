using System;
using System.Linq;
using Wabbajack.Networking.Bethesda;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     The exit codes are a wire format between two processes, so they are pinned rather than derived.
/// </summary>
public class SteamAppTicketErrorTests
{
    private static SteamAppTicketError[] All =>
        Enum.GetValues<SteamAppTicketError>();

    [Fact]
    public void EveryErrorRoundTripsThroughItsExitCode()
    {
        foreach (var error in All)
            Assert.Equal(error, SteamAppTicketErrors.FromExitCode(error.ToExitCode()));
    }

    [Fact]
    public void OnlySuccessExitsZero()
    {
        foreach (var error in All.Where(e => e != SteamAppTicketError.None))
            Assert.True(error.ToExitCode() > 1,
                "an exit code of 1 is what an ordinary CLI failure uses, and 0 is success");
    }

    [Fact]
    public void ExitCodesAreUnique()
    {
        var codes = All.Select(e => e.ToExitCode()).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(255)]
    [InlineData(3221225477)]
    public void AnExitCodeNobodyChoseIsAStatementAboutTheHelper(long exitCode)
    {
        Assert.Equal(SteamAppTicketError.HelperFailed,
            SteamAppTicketErrors.FromExitCode(unchecked((int) exitCode)));
    }

    [Fact]
    public void EveryErrorHasSomethingToSay()
    {
        foreach (var error in All)
        {
            var message = error.DefaultMessage("Skyrim Special Edition");
            Assert.False(string.IsNullOrWhiteSpace(message));
            Assert.EndsWith(".", message);
        }
    }

    [Fact]
    public void TheSentencesAreDifferentFromEachOther()
    {
        // The point of having several errors is that each is a different thing to go and do; two that read
        // the same would be one error wearing two names.
        var messages = All.Select(e => e.DefaultMessage("Skyrim Special Edition")).ToList();
        Assert.Equal(messages.Count, messages.Distinct().Count());
    }
}
