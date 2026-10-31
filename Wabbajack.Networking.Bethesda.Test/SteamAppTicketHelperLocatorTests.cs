using System;
using System.Collections.Generic;
using System.IO;
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

        Assert.Equal(self, command!.FileName);
        Assert.Empty(command.LeadingArguments);
    }

    [Fact]
    public void AReleaseFindsItInTheCliFolder()
    {
        var command = SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe", Only(InTheCliFolder));

        Assert.Equal(InTheCliFolder, command!.FileName);
    }

    [Fact]
    public void ADevelopmentBuildFindsItBesideTheApp()
    {
        var command = SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe",
            Only(BesideTheApp, InTheCliFolder));

        Assert.True(command!.FileName == BesideTheApp, "the copy in the output folder is the one just built");
    }

    [Fact]
    public void AFrameworkDependentLayoutFallsBackToDotnet()
    {
        var command = SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe", Only(AsALibrary));

        Assert.Equal("dotnet", command!.FileName);
        Assert.Equal(new[] {AsALibrary}, command.LeadingArguments);
    }

    [Fact]
    public void AnOverrideBeatsEverythingElse()
    {
        var elsewhere = Path.Combine("/elsewhere", SteamAppTicketHelperLocator.ExecutableName);

        var command = SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe",
            Only(BesideTheApp, elsewhere), elsewhere);

        Assert.Equal(elsewhere, command!.FileName);
    }

    [Fact]
    public void AnOverrideThatIsNotThereIsIgnoredRatherThanFatal()
    {
        var command = SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe", Only(BesideTheApp),
            "/elsewhere/gone.exe");

        Assert.Equal(BesideTheApp, command!.FileName);
    }

    [Fact]
    public void NoCliAnywhereIsNull()
    {
        Assert.Null(SteamAppTicketHelperLocator.Locate(Base, "/app/Wabbajack.exe", Only()));
    }

    [Fact]
    public void ArgumentsGoAfterWhateverTheCommandNeedsInFront()
    {
        var command = new SteamAppTicketHelperCommand("dotnet", new List<string> {"wabbajack-cli.dll"});

        Assert.Equal(new[] {"wabbajack-cli.dll", "steam-app-ticket", "--game", "SkyrimSpecialEdition"},
            command.WithArguments(new[] {"steam-app-ticket", "--game", "SkyrimSpecialEdition"}));
    }
}
