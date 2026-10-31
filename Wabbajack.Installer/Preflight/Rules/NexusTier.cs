using Wabbajack.Networking.NexusApi;

namespace Wabbajack.Installer.Preflight.Rules;

/// <summary>
///     The words the Nexus Mods metrics are reported in. Membership only: nothing here says who the account is.
/// </summary>
public static class NexusTier
{
    public const string Premium = "premium";
    public const string Free = "free";
    public const string LoggedOut = "logged_out";
    public const string Unknown = "unknown";

    /// <summary>
    ///     Null is a probe that never answered. Unknown is kept apart from free so that a dropped connection
    ///     cannot count as an install without premium.
    /// </summary>
    public static string Of(NexusLoginStatus? status)
    {
        if (status == null) return Unknown;
        if (!status.HasToken) return LoggedOut;
        if (!status.LoggedIn) return Unknown;
        return status.IsPremium ? Premium : Free;
    }

    /// <summary>
    ///     Null means there is nothing to report. <paramref name="wasPremium" /> is null the first time an
    ///     account is seen, which is a baseline rather than a conversion: everyone already premium when this
    ///     shipped would otherwise read as having just upgraded.
    /// </summary>
    public static string? Change(bool? wasPremium, bool isPremium)
    {
        if (wasPremium == null || wasPremium == isPremium) return null;
        return isPremium ? "free_to_premium" : "premium_to_free";
    }
}
