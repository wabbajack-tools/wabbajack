#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class InstallFolderProbeTests : IDisposable
{
    private readonly AbsolutePath _root;

    public InstallFolderProbeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wj-folder-probe-" + Guid.NewGuid().ToString("N"))
            .ToAbsolutePath();
        _root.CreateDirectory();
    }

    public void Dispose()
    {
        if (_root.DirectoryExists()) _root.DeleteDirectory();
    }

    private AbsolutePath Install => _root.Combine("install");
    private AbsolutePath Downloads => Install.Combine("downloads");

    private void Write(AbsolutePath file, string text = "x")
    {
        file.Parent.CreateDirectory();
        file.WriteAllTextAsync(text).Wait();
    }

    private InstallFolderEvidence Scan(params string[] modListFiles)
    {
        var destinations = new HashSet<RelativePath>();
        foreach (var f in modListFiles) destinations.Add(f.ToRelativePath());

        return InstallFolderProbe.Scan(new InstallFolderProbeInput("My List", Install, Downloads,
            _root.Combine("My List.wabbajack"), destinations.Contains, destinations.Count,
            new HashSet<AbsolutePath>()));
    }

    [Fact]
    public void MissingFolder_HasNothingAtRisk()
    {
        var evidence = Scan();

        Assert.Equal(0, evidence.FilesAtRisk);
        Assert.Equal(InstallFolderKind.Empty, InstallFolderRules.Classify(evidence).Kind);
    }

    [Fact]
    public void UnrelatedFiles_AreAtRisk()
    {
        Write(Install.Combine("holiday", "photo.jpg"));
        Write(Install.Combine("taxes.xlsx"));

        var evidence = Scan();

        Assert.Equal(2, evidence.FilesAtRisk);
        Assert.Equal(InstallFolderKind.Unrecognized, InstallFolderRules.Classify(evidence).Kind);
    }

    [Fact]
    public void FilesInTheDownloadsFolder_AreNotAtRisk()
    {
        Write(Downloads.Combine("a_mod.7z"));
        Write(Downloads.Combine("a_mod.7z.meta"));

        Assert.Equal(0, Scan().FilesAtRisk);
    }

    [Fact]
    public void WabbajacksOwnScratchFolders_AreNotAtRisk()
    {
        Write(Install.Combine(Consts.InstallTempFolderName, "half-extracted.tmp"));
        Write(Install.Combine(Consts.BSACreationDir, "abc", "loose.dds"));

        Assert.Equal(0, Scan().FilesAtRisk);
    }

    [Fact]
    public void ModListFilesAlreadyThere_AreCountedAsRecognized()
    {
        Write(Install.Combine("mods", "a", "plain.txt"));
        Write(Install.Combine("mods", "b", "other.txt"));
        Write(Install.Combine("stray.txt"));

        var evidence = Scan("mods/a/plain.txt", "mods/b/other.txt");

        Assert.Equal(2, evidence.RecognizedFiles);
        Assert.Equal(1, evidence.FilesAtRisk);
    }

    [Fact]
    public void CountStopsAtTheCap()
    {
        for (var i = 0; i < InstallFolderProbe.MaxFilesCounted + 10; i++)
            Write(Install.Combine("junk", $"{i}.txt"));

        var evidence = Scan();

        Assert.True(evidence.CountTruncated);
        Assert.Equal(InstallFolderProbe.MaxFilesCounted, evidence.FilesAtRisk);
    }

    [Fact]
    public async Task Marker_RoundTrips()
    {
        Install.CreateDirectory();
        await InstallFolderMarker.Write(Install, "My List", "repo/machine-url", "1.2.3.0");

        var marker = InstallFolderMarker.Read(Install);

        Assert.NotNull(marker);
        Assert.Equal("My List", marker!.ModListName);
        Assert.Equal("repo/machine-url", marker.MachineUrl);
        Assert.Equal("1.2.3.0", marker.Version);
        Assert.Equal("My List", Scan().MarkerModListName);
    }

    [Fact]
    public void UnreadableMarker_IsTreatedAsAbsent()
    {
        Write(InstallFolderMarker.PathIn(Install), "this is not json");

        Assert.Null(InstallFolderMarker.Read(Install));
    }

    [Fact]
    public void MarkerIsNotCountedAgainstTheFolder()
    {
        Write(InstallFolderMarker.PathIn(Install), "{\"ModListName\":\"My List\"}");

        var evidence = Scan();

        Assert.Equal("My List", evidence.MarkerModListName);
        Assert.Equal(0, evidence.FilesAtRisk);
    }

    [Theory]
    [InlineData("portable.txt")]
    [InlineData(".wabbajack-install.json")]
    public void TheInstallersOwnFilesAreNotAtRisk(string file)
    {
        Write(Install.Combine(file.ToRelativePath()));
        Write(Install.Combine("stray.txt"));

        Assert.Equal(1, Scan().FilesAtRisk);
    }

    [Fact]
    public void AMarkedFolderHoldingOnlyThisListIsAnUpdateNotAnEmptyFolder()
    {
        Write(InstallFolderMarker.PathIn(Install), "{\"ModListName\":\"My List\"}");
        Write(Install.Combine("portable.txt"));
        Write(Install.Combine("mods", "a", "plain.txt"));
        Write(Install.Combine("mods", "a", "leftover.txt"));

        var verdict = InstallFolderRules.Classify(Scan("mods/a/plain.txt"));

        Assert.Equal(InstallFolderKind.ModListInstall, verdict.Kind);
        Assert.True(verdict.IsSameModList);
    }

    [Fact]
    public void Mo2Fingerprint_NeedsMoreThanTheIniAlone()
    {
        Write(Install.Combine("ModOrganizer.ini"));

        Assert.False(InstallFolderProbe.HasMo2Fingerprint(Install, Downloads));
    }

    [Theory]
    [InlineData("portable.txt")]
    [InlineData("mods/a_mod/meta.ini")]
    public void Mo2Fingerprint_IsTheIniPlusOneCorroborator(string corroborator)
    {
        Write(Install.Combine("ModOrganizer.ini"));
        Write(Install.Combine(corroborator.ToRelativePath()));

        Assert.True(InstallFolderProbe.HasMo2Fingerprint(Install, Downloads));
    }

    [Fact]
    public void Mo2Fingerprint_CountsDownloadMetas()
    {
        Write(Install.Combine("ModOrganizer.ini"));
        Write(Downloads.Combine("a_mod.7z.meta"));

        Assert.True(InstallFolderProbe.HasMo2Fingerprint(Install, Downloads));
    }

    [Fact]
    public void Mo2Fingerprint_IgnoresAModsFolderWithNoModsInIt()
    {
        Write(Install.Combine("ModOrganizer.ini"));
        Install.Combine(Consts.MO2ModFolderName, "empty_mod").CreateDirectory();

        Assert.False(InstallFolderProbe.HasMo2Fingerprint(Install, Downloads));
    }

    [Fact]
    public void SavedInstallLocation_IsOnlyMatchedWhenItIsThisFolder()
    {
        Install.CreateDirectory();

        Assert.True(InstallFolderProbe.Scan(Input(Install)).MatchesSavedInstall);
        Assert.False(InstallFolderProbe.Scan(Input(_root.Combine("somewhere else"))).MatchesSavedInstall);
        Assert.False(InstallFolderProbe.Scan(Input(default)).MatchesSavedInstall);
    }

    private InstallFolderProbeInput Input(AbsolutePath savedInstall)
    {
        return new InstallFolderProbeInput("My List", Install, Downloads, _root.Combine("My List.wabbajack"),
            _ => false, 0, new HashSet<AbsolutePath>(), savedInstall);
    }
}
