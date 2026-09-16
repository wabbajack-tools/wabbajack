using System;
using System.Linq;
using FluentAssertions;
using Wabbajack.Networking.Bethesda.Steam;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

public class SteamAppTicketOutputTests
{
    [Fact]
    public void ATicketSurvivesTheRoundTrip()
    {
        var ticket = Enumerable.Range(0, 159).Select(i => (byte) (i * 7)).ToArray();

        SteamAppTicketOutput.TryParse(SteamAppTicketOutput.Format(ticket), out var read).Should().BeTrue();

        read.Should().Equal(ticket);
    }

    [Fact]
    public void TheTicketIsFoundAmongTheHelpersOwnLogging()
    {
        // The CLI logs to the console as well as to a file, so standard output is shared. This is what that
        // actually looks like.
        var output = string.Join(Environment.NewLine,
            "0.123 [INFO] Asking Steam for an app ticket for Skyrim Special Edition (app 489830)",
            SteamAppTicketOutput.Format(new byte[] {0xDE, 0xAD, 0xBE, 0xEF}),
            "0.456 [INFO] Steam issued a 4 byte app ticket");

        SteamAppTicketOutput.TryParse(output, out var ticket).Should().BeTrue();

        ticket.Should().Equal(0xDE, 0xAD, 0xBE, 0xEF);
    }

    [Fact]
    public void CarriageReturnsDoNotEndUpInTheHex()
    {
        SteamAppTicketOutput.TryParse("ticket 0A0B\r\n", out var ticket).Should().BeTrue();

        ticket.Should().Equal(0x0A, 0x0B);
    }

    [Fact]
    public void TheLastTicketWins()
    {
        var output = SteamAppTicketOutput.Format(new byte[] {1}) + "\n" +
                     SteamAppTicketOutput.Format(new byte[] {2});

        SteamAppTicketOutput.TryParse(output, out var ticket).Should().BeTrue();

        ticket.Should().Equal((byte) 2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0.1 [INFO] nothing to see here")]
    [InlineData("ticket ")]
    [InlineData("ticket not-hex-at-all")]
    [InlineData("ticket 0A0")]
    [InlineData("the ticket is 0A0B")]
    public void AnythingThatIsNotATicketLineIsNotATicket(string? output)
    {
        SteamAppTicketOutput.TryParse(output, out var ticket).Should().BeFalse();

        ticket.Should().BeEmpty();
    }

    [Fact]
    public void AnUnreadableLineDoesNotHideAReadableOne()
    {
        SteamAppTicketOutput.TryParse("ticket zzzz\nticket 0A0B", out var ticket).Should().BeTrue();

        ticket.Should().Equal(0x0A, 0x0B);
    }
}
