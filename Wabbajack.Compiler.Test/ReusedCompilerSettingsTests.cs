using System;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compiler.Test;

public class ReusedCompilerSettingsTests : IDisposable
{
    private readonly TemporaryFileManager _manager;
    private readonly AbsolutePath _here;
    private readonly AbsolutePath _mo2;
    private readonly AbsolutePath _settingsFile;

    public ReusedCompilerSettingsTests()
    {
        _manager = new TemporaryFileManager(KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()));
        _here = _manager.CreateFolder().Path;
        _mo2 = _here.Combine("The List");
        _mo2.CreateDirectory();
        _settingsFile = _mo2.Combine("The List.compiler_settings");
    }

    public void Dispose()
    {
        _manager.Dispose();
    }

    /// <summary>The settings an installed list ships: written on its author's machine.</summary>
    private static CompilerSettings Authors()
    {
        return new CompilerSettings
        {
            Source = @"E:\Author\The List".ToAbsolutePath(),
            Downloads = @"E:\Author\downloads".ToAbsolutePath(),
            OutputFile = @"E:\wj_builds\The List.wabbajack".ToAbsolutePath(),
            ModListImage = @"E:\Author\banner.webp".ToAbsolutePath(),
            ModListName = "The List",
            Profile = "Default",
            MachineUrl = "the_list",
            PublishUpdate = true,
            Include = new[] {(RelativePath) @"mods\Launcher\launcher.bat"}
        };
    }

    [Fact]
    public void PointsAnotherMachinesSettingsAtThisFolder()
    {
        var settings = Authors();

        ReusedCompilerSettings.Repoint(settings, _settingsFile, null);

        Assert.Equal(_mo2, settings.Source);
        Assert.Equal(_mo2.Combine("downloads"), settings.Downloads);
        Assert.Equal(_here.Combine("The List.wabbajack"), settings.OutputFile);
        Assert.Equal(default, settings.ModListImage);
    }

    [Fact]
    public void TakesDownloadsFromMO2WhenItCanReadTheProfile()
    {
        var settings = Authors();
        var mo2Downloads = _here.Combine("shared downloads");

        ReusedCompilerSettings.Repoint(settings, _settingsFile, new CompilerSettings {Downloads = mo2Downloads});

        Assert.Equal(mo2Downloads, settings.Downloads);
    }

    [Fact]
    public void KeepsTheRulesAndClearsTheAuthorsGalleryEntry()
    {
        var settings = Authors();

        var changed = ReusedCompilerSettings.Repoint(settings, _settingsFile, null);

        Assert.Equal(new[] {(RelativePath) @"mods\Launcher\launcher.bat"}, settings.Include);
        Assert.Equal("The List", settings.ModListName);
        Assert.Equal("", settings.MachineUrl);
        Assert.False(settings.PublishUpdate);
        Assert.Contains("machine URL", changed);
    }

    [Fact]
    public void LeavesAUsersOwnSettingsAlone()
    {
        var downloads = _here.Combine("downloads");
        downloads.CreateDirectory();
        var image = _mo2.Combine("banner.png");
        image.WriteAllText("not really a png");
        var settings = new CompilerSettings
        {
            Source = _mo2, Downloads = downloads, OutputFile = _here.Combine("The List.wabbajack"),
            ModListImage = image, MachineUrl = "the_list", PublishUpdate = true
        };

        var changed = ReusedCompilerSettings.Repoint(settings, _settingsFile, null);

        Assert.Empty(changed);
        Assert.Equal("the_list", settings.MachineUrl);
        Assert.True(settings.PublishUpdate);
    }

    [Fact]
    public void AMovedFolderKeepsItsMachineUrl()
    {
        // The author moved their own MO2 folder: only the source is out of date.
        var downloads = _here.Combine("downloads");
        downloads.CreateDirectory();
        var settings = new CompilerSettings
        {
            Source = @"D:\Old Place\The List".ToAbsolutePath(), Downloads = downloads,
            OutputFile = _here.Combine("The List.wabbajack"), MachineUrl = "the_list", PublishUpdate = true
        };

        var changed = ReusedCompilerSettings.Repoint(settings, _settingsFile, null);

        Assert.Equal(new[] {"source"}, changed);
        Assert.Equal("the_list", settings.MachineUrl);
    }
}
