using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compiler.Test;

public class CompilerSettingsInferencerTests : IDisposable
{
    private readonly TemporaryFileManager _manager;
    private readonly AbsolutePath _mo2;

    public CompilerSettingsInferencerTests()
    {
        _manager = new TemporaryFileManager(KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()));
        _mo2 = _manager.CreateFolder().Path;

        // MO2 last had the TTW profile selected; the tests pick other ones.
        File.WriteAllText(_mo2.Combine("ModOrganizer.ini").ToString(),
            "[General]\r\ngameName = New Vegas\r\nselected_profile = @ByteArray(List - Tale of Two Wastelands)\r\n" +
            "[Settings]\r\ndownload_directory = C:/Modlists/List/downloads\r\n");

        foreach (var profile in new[] {"List - New Vegas", "List - Tale of Two Wastelands", "List - Extra"})
        {
            var folder = _mo2.Combine("profiles", profile);
            folder.CreateDirectory();
            File.WriteAllText(folder.Combine("modlist.txt").ToString(), "+Some Mod\r\n");
        }
    }

    public void Dispose()
    {
        _manager.Dispose();
    }

    private Task<CompilerSettings?> InferFrom(string profile)
    {
        return new CompilerSettingsInferencer(NullLogger<CompilerSettingsInferencer>.Instance)
            .InferModListFromLocation(_mo2.Combine("profiles", profile, "modlist.txt"));
    }

    [Fact]
    public async Task UsesTheProfileOfThePickedModlist()
    {
        var settings = await InferFrom("List - New Vegas");

        Assert.NotNull(settings);
        Assert.Equal("List - New Vegas", settings!.Profile);
        Assert.Equal("List - New Vegas", settings.ModListName);
        Assert.Equal(Game.FalloutNewVegas, settings.Game);
    }

    [Fact]
    public async Task KeepsThePickedProfilesOtherProfiles()
    {
        File.WriteAllText(_mo2.Combine("profiles", "List - New Vegas", "otherprofiles.txt").ToString(),
            "List - Extra\r\n");

        var settings = await InferFrom("List - New Vegas");

        Assert.Equal("List - New Vegas", settings!.Profile);
        Assert.Equal(new[] {"List - Extra"}, settings.AdditionalProfiles);
    }
}
