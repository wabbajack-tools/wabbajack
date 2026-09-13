using System.Collections.Generic;
using System.IO;
using Wabbajack.Installer.Preflight;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class CandidateFileTests
{
    private static AbsolutePath P(string name)
    {
        return @"C:\Users\someone\Downloads".ToAbsolutePath().Combine(name);
    }

    [Theory]
    [InlineData("SkyUI.7z.crdownload", true)]
    [InlineData("SkyUI.7z.part", true)]
    [InlineData("SkyUI.7z.partial", true)]
    [InlineData("SkyUI.7z.tmp", true)]
    [InlineData("SkyUI.7z.temp", true)]
    [InlineData("SkyUI.7z.download", true)]
    [InlineData("SkyUI.7z.opdownload", true)]
    [InlineData("SkyUI.7z.aria2", true)]
    [InlineData("SkyUI.7z.!ut", true)]
    [InlineData("SkyUI.7z.!qb", true)]
    [InlineData("abcd1234.wj_incoming", true)]
    [InlineData("SkyUI.7z.meta", true)]
    [InlineData("SkyUI.7Z.CRDOWNLOAD", true)]
    [InlineData("SkyUI.7z", false)]
    [InlineData("SkyUI.zip", false)]
    [InlineData("SkyUI.rar", false)]
    [InlineData("SkyUI", false)]
    [InlineData("part", false)]
    [InlineData("my.partial.archive.7z", false)]
    public void PartialDownloadsAreRecognisedByExtension(string name, bool partial)
    {
        Assert.Equal(partial, CandidateFile.IsPartialDownload(P(name)));
    }

    [Fact]
    public void ACustomPartialSetReplacesTheDefault()
    {
        var custom = new HashSet<Extension> {new(".custom")};
        Assert.True(CandidateFile.IsPartialDownload(P("a.7z.custom"), custom));
        Assert.False(CandidateFile.IsPartialDownload(P("a.7z.crdownload"), custom));
    }

    [Theory]
    [InlineData("SkyUI.7z.part", true)]
    [InlineData("SkyUI.7z.crdownload", true)]
    [InlineData("SkyUI.7z.tmp", true)]
    [InlineData("SkyUI.part", false)]
    [InlineData("Other.7z.part", false)]
    [InlineData("", false)]
    public void APlaceholderIsRecognisedByItsPartialSibling(string existing, bool placeholder)
    {
        var present = existing == "" ? default : P(existing);
        Assert.Equal(placeholder, CandidateFile.HasPartialSibling(P("SkyUI.7z"), p => p == present));
    }

    [Theory]
    [InlineData(FileAttributes.Normal, false)]
    [InlineData(FileAttributes.Archive, false)]
    [InlineData(FileAttributes.ReadOnly, false)]
    [InlineData(FileAttributes.SparseFile, false)]
    [InlineData(FileAttributes.ReparsePoint, false)]
    [InlineData(FileAttributes.Offline, true)]
    [InlineData(FileAttributes.Archive | FileAttributes.Offline, true)]
    [InlineData((FileAttributes) CandidateFile.RecallOnDataAccess, true)]
    [InlineData(FileAttributes.Archive | (FileAttributes) CandidateFile.RecallOnDataAccess, true)]
    [InlineData(FileAttributes.ReparsePoint | FileAttributes.SparseFile, true)]
    [InlineData(FileAttributes.Archive | FileAttributes.ReparsePoint | FileAttributes.SparseFile, true)]
    public void CloudPlaceholdersAreRecognisedByAttributes(FileAttributes attributes, bool placeholder)
    {
        Assert.Equal(placeholder, CandidateFile.IsCloudPlaceholder(attributes));
    }

    [Theory]
    [InlineData(FileAttributes.Normal, false)]
    [InlineData(FileAttributes.Archive, false)]
    [InlineData(FileAttributes.ReadOnly, false)]
    [InlineData(FileAttributes.Hidden, true)]
    [InlineData(FileAttributes.System, true)]
    [InlineData(FileAttributes.Directory, true)]
    [InlineData(FileAttributes.Archive | FileAttributes.Hidden, true)]
    public void HiddenSystemAndDirectoriesAreIgnored(FileAttributes attributes, bool ignored)
    {
        Assert.Equal(ignored, CandidateFile.IsIgnoredAttributes(attributes));
    }

    [Fact]
    public void TheDefaultPartialSetCoversThisApplicationsOwnFiles()
    {
        Assert.Contains(new Extension(".wj_incoming"), CandidateFile.DefaultPartialExtensions);
        Assert.Contains(new Extension(".meta"), CandidateFile.DefaultPartialExtensions);
    }
}
