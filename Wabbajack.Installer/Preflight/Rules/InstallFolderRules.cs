using System;

namespace Wabbajack.Installer.Preflight.Rules;

public enum InstallFolderKind
{
    Empty,
    ModListInstall,
    Unrecognized
}

public sealed record InstallFolderEvidence(
    string ModListName,
    int FilesAtRisk,
    bool CountTruncated,
    int RecognizedFiles,
    int DirectiveCount,
    string? MarkerModListName,
    bool HasMo2Fingerprint,
    bool MatchesSavedInstall,
    string? InstalledVersion = null,
    string? IncomingVersion = null)
{
    public bool HasMarker => MarkerModListName != null;

    public bool MarkerIsThisModList => MarkerModListName != null &&
                                       string.Equals(MarkerModListName, ModListName,
                                           StringComparison.OrdinalIgnoreCase);
}

public sealed record InstallFolderVerdict(InstallFolderKind Kind, string Message, string? Detail = null,
    bool IsSameModList = false);

public static class InstallFolderRules
{
    public const int RecognizedFileFloor = 50;
    public const double RecognizedFractionFloor = 0.25;
    public const double SameModListFractionFloor = 0.9;
    public const int SevereDeletionCount = 10_000;

    public static bool IsSevere(int filesToDelete) => filesToDelete >= SevereDeletionCount;

    public static bool IsSevere(InstallFolderVerdict verdict, int filesToDelete) =>
        !verdict.IsSameModList || IsSevere(filesToDelete);

    public static InstallFolderVerdict Classify(InstallFolderEvidence e)
    {
        if (e.FilesAtRisk == 0)
            return new InstallFolderVerdict(InstallFolderKind.Empty, "Nothing in this folder would be removed");

        var atRisk = Describe(e.FilesAtRisk, e.CountTruncated);

        if (IsThisModList(e))
        {
            var change = ModListVersions.Compare(e.InstalledVersion, e.IncomingVersion);
            var detail = "Your save files and your downloads folder are kept. Anything else that is not part " +
                         "of the modlist is deleted, including mods you added yourself, unless the folder " +
                         "name starts with [NoDelete].";
            if (ModListVersions.Describe(change, e.InstalledVersion, e.IncomingVersion) is { } versions)
                detail = $"{versions}\n\n{detail}";

            return new InstallFolderVerdict(InstallFolderKind.ModListInstall,
                $"{e.ModListName} is already installed here and will be reset to the version you are " +
                $"installing, removing {atRisk}", detail, true);
        }

        if (IsSomeModList(e))
        {
            var holds = e.HasMarker ? e.MarkerModListName : "another modlist";
            return new InstallFolderVerdict(InstallFolderKind.ModListInstall,
                $"This folder holds {holds}, and installing {e.ModListName} will replace it, removing {atRisk}",
                "Your downloads folder is kept, so shared files are not downloaded again. Everything else " +
                "that belongs to the modlist already here, including its save files, is deleted.");
        }

        return new InstallFolderVerdict(InstallFolderKind.Unrecognized,
            $"This folder holds {atRisk} that are not part of any modlist",
            "Choose an empty folder, or a folder an existing modlist is installed in.");
    }

    private static bool IsThisModList(InstallFolderEvidence e)
    {
        if (e.HasMarker) return e.MarkerIsThisModList;
        return e.MatchesSavedInstall || HoldsAlmostNothingElse(e);
    }

    private static bool IsSomeModList(InstallFolderEvidence e) =>
        e.HasMarker || e.HasMo2Fingerprint || e.MatchesSavedInstall || RecognizesEnough(e);

    private static bool HoldsAlmostNothingElse(InstallFolderEvidence e)
    {
        if (e.CountTruncated || e.RecognizedFiles < RecognizedFileFloor) return false;

        var examined = (double) e.RecognizedFiles + e.FilesAtRisk;
        return e.RecognizedFiles / examined >= SameModListFractionFloor;
    }

    private static bool RecognizesEnough(InstallFolderEvidence e)
    {
        if (e.DirectiveCount <= 0 || e.RecognizedFiles == 0) return false;
        if (e.RecognizedFiles >= RecognizedFileFloor) return true;
        return e.RecognizedFiles >= e.DirectiveCount * RecognizedFractionFloor;
    }

    private static string Describe(int files, bool truncated) =>
        truncated ? $"more than {files:N0} files" : $"{files:N0} {(files == 1 ? "file" : "files")}";
}
