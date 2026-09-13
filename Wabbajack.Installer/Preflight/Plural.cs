namespace Wabbajack.Installer.Preflight;

/// <summary>
///     Counts in check messages: "1 file", "2 files". A phrase whose verb changes with the count passes both
///     forms in full, "game file is missing" and "game files are missing".
/// </summary>
internal static class Plural
{
    public static string Of(int count, string singular, string? plural = null)
    {
        return $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
    }
}
