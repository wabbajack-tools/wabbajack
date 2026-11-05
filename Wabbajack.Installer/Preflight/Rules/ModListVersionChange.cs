using System;

namespace Wabbajack.Installer.Preflight.Rules;

public enum VersionChange
{
    Unknown,
    Same,
    Patch,
    Minor,
    Major,
    Downgrade
}

public static class ModListVersions
{
    public static VersionChange Compare(string? installed, string? incoming)
    {
        if (!TryParse(installed, out var from) || !TryParse(incoming, out var to)) return VersionChange.Unknown;

        if (to == from) return VersionChange.Same;
        if (to < from) return VersionChange.Downgrade;
        if (to.Major != from.Major) return VersionChange.Major;
        if (to.Minor != from.Minor) return VersionChange.Minor;
        return VersionChange.Patch;
    }

    public static string? Describe(VersionChange change, string? installed, string? incoming)
    {
        var between = installed != null && incoming != null ? $" ({installed} to {incoming})" : "";

        return change switch
        {
            VersionChange.Major =>
                $"The version number changes in its first part{between}. Under semantic versioning that means " +
                "breaking changes, so this update is likely not save-safe. Not every modlist follows that " +
                "convention, so check the modlist's website or Discord before you continue.",
            VersionChange.Minor or VersionChange.Patch =>
                $"The version number changes in a later part{between}, which usually means an update that " +
                "keeps working with your saves. Check the modlist's website or Discord if you are unsure.",
            VersionChange.Downgrade =>
                $"This installs an older version than the one already here{between}.",
            VersionChange.Same =>
                $"This reinstalls the version already here{(installed != null ? $" ({installed})" : "")}.",
            _ => null
        };
    }

    private static bool TryParse(string? value, out Version version)
    {
        version = new Version();
        return !string.IsNullOrWhiteSpace(value) && Version.TryParse(value, out version!);
    }
}
