#nullable enable
using Wabbajack.Installer.Preflight.Rules;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class InstallFolderRulesTests
{
    private const string ThisList = "My List";

    private static InstallFolderEvidence Evidence(int filesAtRisk = 1, int recognized = 0, int directives = 1000,
        string? marker = null, bool mo2 = false, bool savedInstall = false, bool truncated = false,
        string modListName = ThisList, string? installedVersion = null, string? incomingVersion = null)
    {
        return new InstallFolderEvidence(modListName, filesAtRisk, truncated, recognized, directives, marker, mo2,
            savedInstall, installedVersion, incomingVersion);
    }

    private static InstallFolderKind Kind(InstallFolderEvidence e) => InstallFolderRules.Classify(e).Kind;

    [Fact]
    public void NothingAtRisk_IsEmpty()
    {
        Assert.Equal(InstallFolderKind.Empty, Kind(Evidence(filesAtRisk: 0)));
    }

    [Fact]
    public void NothingAtRisk_IsEmptyEvenWithFilesPresent()
    {
        Assert.Equal(InstallFolderKind.Empty, Kind(Evidence(filesAtRisk: 0, recognized: 5000)));
    }

    [Fact]
    public void FolderFullOfUnrelatedFiles_IsRefused()
    {
        Assert.Equal(InstallFolderKind.Unrecognized, Kind(Evidence(filesAtRisk: 3_888_890, directives: 5800)));
    }

    [Fact]
    public void FolderFullOfUnrelatedFiles_IsRefusedEvenWithAHandfulRecognized()
    {
        Assert.Equal(InstallFolderKind.Unrecognized,
            Kind(Evidence(filesAtRisk: 3_888_890, recognized: 12, directives: 5800)));
    }

    [Theory]
    [InlineData(50, 1000)]
    [InlineData(250, 1000)]
    [InlineData(5, 20)]
    public void EnoughRecognizedFiles_SaysAModListIsHereButNeverWhichOne(int recognized, int directives)
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 300, recognized: recognized,
            directives: directives));

        Assert.Equal(InstallFolderKind.ModListInstall, verdict.Kind);
        Assert.False(verdict.IsSameModList);
    }

    [Fact]
    public void TwoUnrelatedMo2ListsShareHundredsOfPaths_SoAPathMatchIsNotAnUpdate()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 8_407, recognized: 861, directives: 5800,
            mo2: true));

        Assert.Equal(InstallFolderKind.ModListInstall, verdict.Kind);
        Assert.False(verdict.IsSameModList);
        Assert.Contains("will replace it", verdict.Message);
    }

    [Theory]
    [InlineData(49, 1000)]
    [InlineData(4, 20)]
    [InlineData(0, 1000)]
    public void TooFewRecognizedFiles_IsNotEnoughOnItsOwn(int recognized, int directives)
    {
        Assert.Equal(InstallFolderKind.Unrecognized,
            Kind(Evidence(filesAtRisk: 300, recognized: recognized, directives: directives)));
    }

    [Fact]
    public void MarkerForThisList_IsThisModList()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 300, marker: ThisList));

        Assert.Equal(InstallFolderKind.ModListInstall, verdict.Kind);
        Assert.True(verdict.IsSameModList);
    }

    [Fact]
    public void MarkerForThisList_IgnoresCase()
    {
        Assert.True(InstallFolderRules.Classify(Evidence(filesAtRisk: 300, marker: "my list")).IsSameModList);
    }

    [Fact]
    public void MarkerForAnotherList_IsAModListBeingReplaced()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 300, marker: "Another List"));

        Assert.Equal(InstallFolderKind.ModListInstall, verdict.Kind);
        Assert.False(verdict.IsSameModList);
        Assert.Contains("Another List", verdict.Message);
    }

    [Fact]
    public void Mo2Fingerprint_IsAModListBeingReplaced()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 300, mo2: true));

        Assert.Equal(InstallFolderKind.ModListInstall, verdict.Kind);
        Assert.False(verdict.IsSameModList);
    }

    [Fact]
    public void AnUpdateWarnsThatAddedModsGoUnlessTheyAreMarkedNoDelete()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 300, recognized: 5800, directives: 5800));

        Assert.True(verdict.IsSameModList);
        Assert.Contains("will be reset to the version you are installing", verdict.Message);
        Assert.Contains("mods you added yourself", verdict.Detail!);
        Assert.Contains("[NoDelete]", verdict.Detail!);
        Assert.Contains("save files", verdict.Detail!);
    }

    [Fact]
    public void AMajorVersionJump_SaysItIsLikelyNotSaveSafe()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 300, recognized: 5800, directives: 5800,
            marker: ThisList, installedVersion: "1.4.0.0", incomingVersion: "2.0.0.0"));

        Assert.Contains("likely not save-safe", verdict.Detail!);
        Assert.Contains("[NoDelete]", verdict.Detail!);
    }

    [Fact]
    public void AMinorVersionBump_SaysItUsuallyKeepsSaves()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 300, recognized: 5800, directives: 5800,
            marker: ThisList, installedVersion: "1.4.0.0", incomingVersion: "1.5.0.0"));

        Assert.Contains("keeps working with your saves", verdict.Detail!);
    }

    [Fact]
    public void NoVersionOnEitherSide_SaysNothingAboutVersions()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 300, recognized: 5800, directives: 5800));

        Assert.DoesNotContain("save-safe", verdict.Detail!);
        Assert.DoesNotContain("version number", verdict.Detail!);
    }

    [Fact]
    public void ReplacingADifferentList_SaysNothingAboutVersions()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 8407, recognized: 861, directives: 5800,
            marker: "Another List", installedVersion: "1.0.0.0", incomingVersion: "2.0.0.0"));

        Assert.False(verdict.IsSameModList);
        Assert.DoesNotContain("save-safe", verdict.Detail!);
    }

    [Fact]
    public void AFolderThatIsAlmostEntirelyThisModList_IsAnUpdateEvenWithNoMarker()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 1, recognized: 5800, directives: 5800,
            mo2: true));

        Assert.Equal(InstallFolderKind.ModListInstall, verdict.Kind);
        Assert.True(verdict.IsSameModList);
        Assert.Contains("is already installed here", verdict.Message);
    }

    [Theory]
    [InlineData(5800, 1)]
    [InlineData(900, 100)]
    [InlineData(50, 5)]
    public void NinetyPercentOfTheFolderBeingOurs_IsEnough(int recognized, int atRisk)
    {
        Assert.True(InstallFolderRules.Classify(Evidence(filesAtRisk: atRisk, recognized: recognized,
            directives: 5800)).IsSameModList);
    }

    [Theory]
    [InlineData(861, 8407)]
    [InlineData(5000, 1000)]
    [InlineData(49, 1)]
    public void LessThanNinetyPercent_OrTooFewFiles_IsNotAnUpdate(int recognized, int atRisk)
    {
        Assert.False(InstallFolderRules.Classify(Evidence(filesAtRisk: atRisk, recognized: recognized,
            directives: 5800)).IsSameModList);
    }

    [Fact]
    public void ATruncatedCount_NeverCountsAsAnUpdate()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 5000, recognized: 100_000,
            directives: 100_000, truncated: true));

        Assert.False(verdict.IsSameModList);
    }

    [Fact]
    public void AMarkerForAnotherList_BeatsTheFolderLookingLikeOurs()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 1, recognized: 5800, directives: 5800,
            marker: "Another List"));

        Assert.False(verdict.IsSameModList);
    }

    [Fact]
    public void SavedInstallLocation_IsThisModList()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 300, savedInstall: true));

        Assert.Equal(InstallFolderKind.ModListInstall, verdict.Kind);
        Assert.True(verdict.IsSameModList);
    }

    [Fact]
    public void AMarkerForAnotherList_BeatsThisMachinesSavedInstallRecord()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: 300, marker: "Another List",
            savedInstall: true));

        Assert.False(verdict.IsSameModList);
        Assert.Contains("Another List", verdict.Message);
    }

    [Theory]
    [InlineData(1, true, false)]
    [InlineData(9_999, true, false)]
    [InlineData(10_000, true, true)]
    [InlineData(1, false, true)]
    public void ReplacingAnotherModList_IsAlwaysSevere(int files, bool sameModList, bool expected)
    {
        var verdict = new InstallFolderVerdict(InstallFolderKind.ModListInstall, "", null, sameModList);

        Assert.Equal(expected, InstallFolderRules.IsSevere(verdict, files));
    }

    [Fact]
    public void NoDirectives_CannotBeRecognizedByMatchAlone()
    {
        Assert.Equal(InstallFolderKind.Unrecognized,
            Kind(Evidence(filesAtRisk: 300, recognized: 500, directives: 0)));
    }

    [Fact]
    public void TruncatedCount_SaysSoInTheMessage()
    {
        var verdict = InstallFolderRules.Classify(Evidence(filesAtRisk: InstallFolderProbe.MaxFilesCounted,
            truncated: true));

        Assert.Contains("more than", verdict.Message);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(9_999, false)]
    [InlineData(10_000, true)]
    [InlineData(3_888_890, true)]
    public void SevereIsReservedForLargeDeletions(int files, bool expected)
    {
        Assert.Equal(expected, InstallFolderRules.IsSevere(files));
    }
}
