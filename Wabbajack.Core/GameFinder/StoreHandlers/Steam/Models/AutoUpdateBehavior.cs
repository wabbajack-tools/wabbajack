using JetBrains.Annotations;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Automatic update behavior.
/// </summary>
/// <remarks>
/// This data was sourced from Steam itself. You can manually change the update
/// settings and verify the values in the <c>*.acf</c> file.
/// </remarks>
[PublicAPI]
public enum AutoUpdateBehavior : byte
{
    /// <summary>
    /// Always keep the app updated. The app and its updates will be
    /// automatically acquired as soon as they are available.
    /// </summary>
    AlwaysUpdated = 0,

    /// <summary>
    /// Only update the app when it's launched. Updated content will
    /// be acquired only when launching the app.
    /// </summary>
    UpdateOnLaunch = 1,

    /// <summary>
    /// High Priority - Always auto-update the app before others. The app
    /// and its updates will be automatically acquired as soon as they
    /// are available. Steam will prioritize the app over other downloads.
    /// </summary>
    HighPriority = 2,
}
