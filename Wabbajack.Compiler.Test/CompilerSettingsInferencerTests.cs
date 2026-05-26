using System.Threading.Tasks;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compiler.Test;

public class CompilerSettingsInferencerTests
{
    private readonly CompilerSettingsInferencer _inferencer;
    private readonly TemporaryFileManager _manager;
    private readonly DTOSerializer _dtos;

    public CompilerSettingsInferencerTests(CompilerSettingsInferencer inferencer, TemporaryFileManager manager, DTOSerializer dtos)
    {
        _inferencer = inferencer;
        _manager = manager;
        _dtos = dtos;
    }

    [Fact]
    public async Task LoadOrInfer_WritesSettingsFileWhenNoneExists()
    {
        using var tempDir = _manager.CreateFolder();
        var mo2Root = tempDir.Path;

        var profileDir = mo2Root.Combine(Consts.MO2Profiles, "Default");
        profileDir.CreateDirectory();
        profileDir.Combine(Consts.ModListTxt).WriteAllText("");
        mo2Root.Combine(Consts.MO2IniName).WriteAllText(
            "[General]\ngameName=Fallout4\nselected_profile=Default\n[Settings]\ndownload_directory=\n");

        await _inferencer.LoadOrInferFromRootPath(mo2Root, _dtos);

        Assert.True(mo2Root.Combine(Consts.CompilerSettings).FileExists());
    }

    [Fact]
    public async Task LoadOrInfer_ReadsExistingSettingsFileWithoutInferring()
    {
        // No MO2 structure — inference would return null. A settings file exists.
        using var tempDir = _manager.CreateFolder();
        var dir = tempDir.Path;

        dir.Combine("compiler_settings.json").WriteAllText("{\"NoMatchInclude\":[\"tools\"]}");

        var result = await _inferencer.LoadOrInferFromRootPath(dir, _dtos);

        Assert.NotNull(result);
        Assert.Single(result!.NoMatchInclude);
        Assert.Equal("tools", result.NoMatchInclude[0].ToString());
    }
}
