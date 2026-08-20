using System.Runtime.InteropServices;
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
        using var tempDir = _manager.CreateFolder();
        var dir = tempDir.Path;

        dir.Combine("compiler_settings.json").WriteAllText("{\"NoMatchInclude\":[\"tools\"]}");

        var result = await _inferencer.LoadOrInferFromRootPath(dir, _dtos);

        Assert.NotNull(result);
        Assert.Single(result!.NoMatchInclude);
        Assert.Equal("tools", result.NoMatchInclude[0].ToString());
    }

    [Fact]
    public async Task InferModList_ReadsGamePathFromMO2Ini()
    {
        using var tempDir = _manager.CreateFolder();
        var mo2Root = tempDir.Path;

        var profileDir = mo2Root.Combine(Consts.MO2Profiles, "TestProfile");
        profileDir.CreateDirectory();
        profileDir.Combine(Consts.ModListTxt).WriteAllText("");
        mo2Root.Combine(Consts.MO2IniName).WriteAllText(
            "[General]\ngameName=Fallout4\nselected_profile=TestProfile\ngamePath=C:\\\\Games\\\\Fallout4\n[Settings]\ndownload_directory=\n");

        var result = await _inferencer.InferModListFromLocation(
            mo2Root.Combine(Consts.MO2Profiles, "TestProfile", Consts.ModListTxt));

        Assert.NotNull(result);
        Assert.NotEqual(default, result!.GamePath);
    }

    [Fact]
    public async Task InferModList_GamePathAsDirectory_StoredAsDirectory()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        using var tempDir = _manager.CreateFolder();
        var mo2Root = tempDir.Path;

        var profileDir = mo2Root.Combine(Consts.MO2Profiles, "TestProfile");
        profileDir.CreateDirectory();
        profileDir.Combine(Consts.ModListTxt).WriteAllText("");
        mo2Root.Combine(Consts.MO2IniName).WriteAllText(
            "[General]\ngameName=Fallout4\nselected_profile=TestProfile\ngamePath=Z:\\\\home\\\\user\\\\Games\\\\Fallout4\n[Settings]\ndownload_directory=\n");

        var result = await _inferencer.InferModListFromLocation(
            mo2Root.Combine(Consts.MO2Profiles, "TestProfile", Consts.ModListTxt));

        Assert.NotNull(result);
        Assert.Equal("/home/user/Games/Fallout4", result!.GamePath.ToString());
    }

    [Fact]
    public async Task InferModList_GamePathAsExe_StoredAsParentDirectory()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        using var tempDir = _manager.CreateFolder();
        var mo2Root = tempDir.Path;

        var profileDir = mo2Root.Combine(Consts.MO2Profiles, "TestProfile");
        profileDir.CreateDirectory();
        profileDir.Combine(Consts.ModListTxt).WriteAllText("");
        mo2Root.Combine(Consts.MO2IniName).WriteAllText(
            "[General]\ngameName=Fallout4\nselected_profile=TestProfile\ngamePath=Z:\\\\home\\\\user\\\\Games\\\\Fallout4\\\\Fallout4.exe\n[Settings]\ndownload_directory=\n");

        var result = await _inferencer.InferModListFromLocation(
            mo2Root.Combine(Consts.MO2Profiles, "TestProfile", Consts.ModListTxt));

        Assert.NotNull(result);
        Assert.Equal("/home/user/Games/Fallout4", result!.GamePath.ToString());
    }

    [Fact]
    public async Task InferModList_GamePathByteArrayFormat_StoredAsDirectory()
    {
        // MO2 on Linux writes gamePath using Qt's @ByteArray encoding with Z:\\ prefix,
        // which produces 4 backslashes before the first path component in the ini file.
        // This is the format FromMO2Ini routes through UnescapeUTF8 (not Regex.Unescape).
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        using var tempDir = _manager.CreateFolder();
        var mo2Root = tempDir.Path;

        var profileDir = mo2Root.Combine(Consts.MO2Profiles, "TestProfile");
        profileDir.CreateDirectory();
        profileDir.Combine(Consts.ModListTxt).WriteAllText("");
        // 4 actual backslashes before 'home', 2 between each subsequent component
        mo2Root.Combine(Consts.MO2IniName).WriteAllText(
            "[General]\ngameName=Fallout4\nselected_profile=TestProfile\ngamePath=@ByteArray(Z:\\\\\\\\home\\\\user\\\\Games\\\\Fallout4)\n[Settings]\ndownload_directory=\n");

        var result = await _inferencer.InferModListFromLocation(
            mo2Root.Combine(Consts.MO2Profiles, "TestProfile", Consts.ModListTxt));

        Assert.NotNull(result);
        Assert.Equal("/home/user/Games/Fallout4", result!.GamePath.ToString());
    }
}
