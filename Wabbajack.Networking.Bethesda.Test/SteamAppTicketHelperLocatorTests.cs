using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Wabbajack.Networking.Bethesda.Steam;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

public class SteamAppTicketHelperLocatorTests
{
    private const string Base = "/app";

    private static readonly string BesideTheApp = Path.Combine(Base, SteamAppTicketHelperLocator.ExecutableName);

    private static readonly string InTheCliFolder =
        Path.Combine(Base, "cli", SteamAppTicketHelperLocator.ExecutableName);

    private static readonly string AsALibrary =
        Path.Combine(Base, SteamAppTicketHelperLocator.HelperName + ".dll");

    private static Func<string, bool> Only(params string[] present)
    {
        var set = new HashSet<string>(present, StringComparer.OrdinalIgnoreCase);
        return path => set.Contains(path);
    }

    [Fact]
    public void TheCliRunningItselfNeedsNoSearch()
    {
        var self = Path.Combine("/somewhere/else", SteamAppTicketHelperLocator.ExecutableName);

        var command = SteamAppTicketHelperLocator.Locate(Base, self, Only());

        command!.FileName.Should().Be(self);
        command.LeadingArguments.Should().BeEmpty();
    }

    [Fact]
    public void AReleaseFindsItInTheCliFolder()
    {
        var command = SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe", Only(InTheCliFolder));

        command!.FileName.Should().Be(InTheCliFolder);
    }

    [Fact]
    public void ADevelopmentBuildFindsItBesideTheApp()
    {
        var command = SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe",
            Only(BesideTheApp, InTheCliFolder));

        command!.FileName.Should().Be(BesideTheApp, "the copy in the output folder is the one just built");
    }

    [Fact]
    public void AFrameworkDependentLayoutFallsBackToDotnet()
    {
        var command = SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe", Only(AsALibrary));

        command!.FileName.Should().Be("dotnet");
        command.LeadingArguments.Should().Equal(AsALibrary);
    }

    [Fact]
    public void AnOverrideBeatsEverythingElse()
    {
        var elsewhere = Path.Combine("/elsewhere", SteamAppTicketHelperLocator.ExecutableName);

        var command = SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe",
            Only(BesideTheApp, elsewhere), elsewhere);

        command!.FileName.Should().Be(elsewhere);
    }

    [Fact]
    public void AnOverrideThatIsNotThereIsIgnoredRatherThanFatal()
    {
        var command = SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe", Only(BesideTheApp),
            "/elsewhere/gone.exe");

        command!.FileName.Should().Be(BesideTheApp);
    }

    [Fact]
    public void NoCliAnywhereIsNull()
    {
        SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe", Only()).Should().BeNull();
    }

    [Fact]
    public void ArgumentsGoAfterWhateverTheCommandNeedsInFront()
    {
        var command = new SteamAppTicketHelperCommand("dotnet", new List<string> {"wabbajack-cli.dll"});

        command.WithArguments(new[] {"steam-app-ticket", "--game", "SkyrimSpecialEdition"})
            .Should().Equal("wabbajack-cli.dll", "steam-app-ticket", "--game", "SkyrimSpecialEdition");
    }
}
