using System;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Local user data for a specific game.
/// </summary>
/// <seealso cref="LocalUserConfig"/>
[PublicAPI]
public sealed record LocalAppData
{
    /// <summary>
    /// Gets the unique identifier of the app that is associated with this data.
    /// </summary>
    public required AppId AppId { get; init; }

    /// <summary>
    /// Gets the last played date or <see cref="DateTimeOffset.UnixEpoch"/>.
    /// </summary>
    public DateTimeOffset LastPlayed { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Gets the playtime or <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public TimeSpan Playtime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the custom launch options for the game or <see cref="string.Empty"/>.
    /// </summary>
    public string LaunchOptions { get; init; } = string.Empty;
}
