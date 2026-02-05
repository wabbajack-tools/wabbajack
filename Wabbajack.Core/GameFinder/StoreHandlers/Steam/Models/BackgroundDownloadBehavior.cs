using JetBrains.Annotations;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Background download behavior.
/// </summary>
/// <remarks>
/// This data was sourced from Steam itself. You can manually change the update
/// settings and verify the values in the <c>*.acf</c> file.
/// </remarks>
[PublicAPI]
public enum BackgroundDownloadBehavior : byte
{
    /// <summary>
    /// Follows the global Steam download settings. The default value
    /// allows downloads while the app is running.
    /// </summary>
    FollowGlobalSteamSettings = 0,

    /// <summary>
    /// Always allow background downloads while the app is running.
    /// This overwrites the global settings.
    /// </summary>
    AlwaysAllow = 1,

    /// <summary>
    /// Never allow background downloads while the app is running.
    /// This overwrites the global settings.
    /// </summary>
    NeverAllow = 2,
}
