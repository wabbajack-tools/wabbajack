using System;
using System.Linq;
using FluentAssertions;
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
            SteamAppTicketErrors.FromExitCode(error.ToExitCode()).Should().Be(error);
    }

    [Fact]
    public void OnlySuccessExitsZero()
    {
        foreach (var error in All.Where(e => e != SteamAppTicketError.None))
            error.ToExitCode().Should().BeGreaterThan(1,
                "an exit code of 1 is what an ordinary CLI failure uses, and 0 is success");
    }

    [Fact]
    public void ExitCodesAreUnique()
    {
        All.Select(e => e.ToExitCode()).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(255)]
    [InlineData(3221225477)]
    public void AnExitCodeNobodyChoseIsAStatementAboutTheHelper(long exitCode)
    {
        SteamAppTicketErrors.FromExitCode(unchecked((int) exitCode))
            .Should().Be(SteamAppTicketError.HelperFailed);
    }

    [Fact]
    public void EveryErrorHasSomethingToSay()
    {
        foreach (var error in All)
        {
            var message = error.DefaultMessage("Skyrim Special Edition");
            message.Should().NotBeNullOrWhiteSpace();
            message.Should().EndWith(".");
        }
    }

    [Fact]
    public void TheSentencesAreDifferentFromEachOther()
    {
        // The point of having several errors is that each is a different thing to go and do; two that read
        // the same would be one error wearing two names.
        All.Select(e => e.DefaultMessage("Skyrim Special Edition")).Should().OnlyHaveUniqueItems();
    }
}
