using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Preflight.Rules;

public sealed record InstallFolderProbeInput(
    string ModListName,
    AbsolutePath Install,
    AbsolutePath Downloads,
    AbsolutePath ModListArchive,
    Func<RelativePath, bool> IsInModList,
    int DirectiveCount,
    IReadOnlySet<AbsolutePath> BsaPathsToNotBuild,
    AbsolutePath SavedInstallLocation = default,
    string? ModListVersion = null);

public static class InstallFolderProbe
{
    public const int MaxFilesCounted = 5_000;

    public static bool IsInstallerScratch(AbsolutePath file, AbsolutePath install)
    {
        return file.InFolder(install.Combine(Consts.InstallTempFolderName)) ||
               file.InFolder(install.Combine(Consts.BSACreationDir));
    }

    public static InstallFolderEvidence Scan(InstallFolderProbeInput input)
    {
        var marker = InstallFolderMarker.Read(input.Install);
        var mo2 = HasMo2Fingerprint(input.Install, input.Downloads);
        var savedMatch = input.SavedInstallLocation != default &&
                         input.SavedInstallLocation.ToString().Length > 0 &&
                         input.SavedInstallLocation == input.Install;

        var (atRisk, recognized, truncated) = Count(input);

        return new InstallFolderEvidence(input.ModListName, atRisk, truncated, recognized, input.DirectiveCount,
            marker?.ModListName, mo2, savedMatch, marker?.Version, input.ModListVersion);
    }

    private static (int AtRisk, int Recognized, bool Truncated) Count(InstallFolderProbeInput input)
    {
        if (!input.Install.DirectoryExists()) return (0, 0, false);

        var atRisk = 0;
        var recognized = 0;

        foreach (var file in input.Install.EnumerateFiles())
        {
            if (input.IsInModList(file.RelativeTo(input.Install)))
            {
                recognized++;
                continue;
            }

            if (IsInstallerScratch(file, input.Install)) continue;

            if (FileDeletionRules.ShouldDeleteExistingFile(file, input.Install, input.Downloads,
                    input.ModListArchive, input.IsInModList, input.BsaPathsToNotBuild))
                atRisk++;

            if (atRisk >= MaxFilesCounted) return (atRisk, recognized, true);
        }

        return (atRisk, recognized, false);
    }

    public static bool HasMo2Fingerprint(AbsolutePath install, AbsolutePath downloads)
    {
        if (!install.Combine("ModOrganizer.ini").FileExists()) return false;

        if (install.Combine("portable.txt").FileExists()) return true;
        if (HasModMetas(install.Combine(Consts.MO2ModFolderName))) return true;
        return HasDownloadMetas(downloads);
    }

    private static bool HasModMetas(AbsolutePath mods)
    {
        if (!mods.DirectoryExists()) return false;

        try
        {
            return mods.EnumerateDirectories(false).Any(mod => mod.Combine("meta.ini").FileExists());
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool HasDownloadMetas(AbsolutePath downloads)
    {
        if (!downloads.DirectoryExists()) return false;

        try
        {
            return downloads.EnumerateFiles(Ext.Meta, false).Any();
        }
        catch (Exception)
        {
            return false;
        }
    }
}
