using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Represents a parsed local user config.
/// </summary>
/// <remarks>
/// <c>userdata/{steamId}/config/localconfig.vdf</c>
/// </remarks>
[PublicAPI]
public sealed record LocalUserConfig
{
    /// <summary>
    /// Gets the absolute path to the parsed config file.
    /// </summary>
    public required AbsolutePath ConfigPath { get; init; }

    /// <summary>
    /// Gets the user that is associated with this config.
    /// </summary>
    public required SteamId User { get; init; }

    /// <summary>
    /// Gets all local user data for almost every game the user owns.
    /// </summary>
    /// <remarks>
    /// It doesn't look like every game the user owns is listed in the config. Games that
    /// the user hasn't played yet aren't listed, for example.
    /// </remarks>
    public required IReadOnlyDictionary<AppId, LocalAppData> LocalAppData { get; init; }

    /// <summary>
    /// Gets the absolute path to the directory where uncompressed screenshots are being saved to.
    /// </summary>
    public AbsolutePath? InGameOverlayScreenshotSaveUncompressedPath { get; init; }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("{ ");
        sb.Append($"ConfigPath = {ConfigPath}, ");
        sb.Append($"User = {User}");
        sb.Append(" }");
        return sb.ToString();
    }

    /// <inheritdoc/>
    public bool Equals(LocalUserConfig? other)
    {
        if (other is null) return false;
        if (User != other.User) return false;
        if (!LocalAppData.SequenceEqual(other.LocalAppData)) return false;
        if (InGameOverlayScreenshotSaveUncompressedPath != other.InGameOverlayScreenshotSaveUncompressedPath) return false;
        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(User);
        hashCode.Add(LocalAppData);
        hashCode.Add(InGameOverlayScreenshotSaveUncompressedPath);
        return hashCode.ToHashCode();
    }
}
