using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using FluentResults;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using Wabbajack.GameFinder.StoreHandlers.Steam.Services;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;
using Wabbajack.GameFinder.Paths.Extensions;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Represents a parsed app manifest file.
/// </summary>
/// <remarks>
/// Manifest files <c>appmanifest_*.acf</c> use Valve's custom
/// KeyValue format.
/// </remarks>
[PublicAPI]
public sealed record AppManifest
{
    /// <summary>
    /// Gets the <see cref="AbsolutePath"/> to the <c>appmanifest_*.acf</c> file
    /// that was parsed to produce this <see cref="AppManifest"/>.
    /// </summary>
    /// <example><c>E:/SteamLibrary/steamapps/appmanifest_262060.acf</c></example>
    /// <seealso cref="InstallationDirectory"/>
    public required AbsolutePath ManifestPath { get; init; }

    #region Parsed Values

    /// <summary>
    /// Gets the unique identifier of the app.
    /// </summary>
    public required AppId AppId { get; init; }

    /// <summary>
    /// Gets the <see cref="SteamUniverse"/> this app is part of. This is pretty irrelevant.
    /// </summary>
    public SteamUniverse Universe { get; init; }

    /// <summary>
    /// Gets name of the app.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the current state of the app.
    /// </summary>
    public required StateFlags StateFlags { get; init; }

    /// <summary>
    /// Gets the <see cref="AbsolutePath"/> to the installation directory of the app.
    /// </summary>
    public required AbsolutePath InstallationDirectory { get; init; }

    /// <summary>
    /// Gets the time when the app was last updated.
    /// </summary>
    /// <remarks>
    /// This value is saved as a unix timestamp in the <c>*.acf</c> file.
    /// </remarks>
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Gets the size of the app on disk.
    /// </summary>
    /// <remarks>
    /// This value is only set when installing or updating the app. If the
    /// user adds or removes files from the <see cref="InstallationDirectory"/>, Steam
    /// won't update this value automatically. This value will be <see cref="Size.Zero"/>
    /// while the app is being staged.
    /// </remarks>
    /// <seealso cref="StagingSize"/>
    public Size SizeOnDisk { get; init; } = Size.Zero;

    /// <summary>
    /// Gets the size of the app during staging.
    /// </summary>
    /// <remarks>
    /// This value will be <see cref="Size.Zero"/> after the app has been
    /// completely downloaded and installed.
    /// </remarks>
    /// <seealso cref="SizeOnDisk"/>
    public Size StagingSize { get; init; } = Size.Zero;

    /// <summary>
    /// Gets the unique identifier of the current build of the app.
    /// </summary>
    /// <remarks>
    /// This value represents the current "patch" of the app and is
    /// a global identifier that can be used to retrieve the current
    /// update notes using SteamDB.
    /// </remarks>
    /// <seealso cref="GetCurrentUpdateNotesUrl"/>
    /// <seealso cref="TargetBuildId"/>
    public BuildId BuildId { get; init; } = BuildId.DefaultValue;

    /// <summary>
    /// Gets the last owner of this app.
    /// </summary>
    /// <remarks>
    /// This is usually the last account that installed and launched the app. This
    /// can be used to get the user date for the current app.
    /// </remarks>
    public SteamId LastOwner { get; init; } = SteamId.Empty;

    /// <summary>
    /// Unknown.
    /// </summary>
    /// <remarks>
    /// The meaning of this value is unknown.
    /// </remarks>
    public uint UpdateResult { get; init; }

    /// <summary>
    /// Gets the amount of bytes to download.
    /// </summary>
    /// <remarks>This value will be <see cref="Size.Zero"/> when there is no update available.</remarks>
    /// <seealso cref="BytesDownloaded"/>
    public Size BytesToDownload { get; init; } = Size.Zero;

    /// <summary>
    /// Gets the amount of bytes downloaded.
    /// </summary>
    /// <remarks>This value will be <see cref="Size.Zero"/> when there is no update available.</remarks>
    /// <seealso cref="BytesToDownload"/>
    public Size BytesDownloaded { get; init; } = Size.Zero;

    /// <summary>
    /// Gets the amount of bytes to stage.
    /// </summary>
    /// <remarks>This value will be <see cref="Size.Zero"/> when there is no update available.</remarks>
    /// <seealso cref="BytesStaged"/>
    public Size BytesToStage { get; init; } = Size.Zero;

    /// <summary>
    /// Gets the amount of bytes staged.
    /// </summary>
    /// <remarks>This value will be <see cref="Size.Zero"/> when there is no update available.</remarks>
    /// <seealso cref="BytesToStage"/>
    public Size BytesStaged { get; init; } = Size.Zero;

    /// <summary>
    /// Gets the target build ID of the update.
    /// </summary>
    /// <remarks>
    /// This value will be <c>0</c>, if there is no update available.
    /// </remarks>
    /// <seealso cref="GetNextUpdateNotesUrl"/>
    /// <seealso cref="BuildId"/>
    public BuildId TargetBuildId { get; init; } = BuildId.DefaultValue;

    /// <summary>
    /// Gets the automatic update behavior for this app.
    /// </summary>
    public AutoUpdateBehavior AutoUpdateBehavior { get; init; }

    /// <summary>
    /// Gets the background download behavior for this app.
    /// </summary>
    public BackgroundDownloadBehavior BackgroundDownloadBehavior { get; init; }

    /// <summary>
    /// Gets the time when the app is scheduled to be updated.
    /// </summary>
    /// <remarks>
    /// The <c>*.acf</c> file saves this value as a unix timestamp and the value will be
    /// <c>0</c> or <see cref="DateTimeOffset.UnixEpoch"/> if there is no update scheduled.
    /// </remarks>
    public DateTimeOffset ScheduledAutoUpdate { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Whether Steam will do a full validation after the next update.
    /// </summary>
    public bool FullValidateAfterNextUpdate { get; init; }

    /// <summary>
    /// Gets all locally installed depots.
    /// </summary>
    public IReadOnlyDictionary<DepotId, InstalledDepot> InstalledDepots { get; init; } = ImmutableDictionary<DepotId, InstalledDepot>.Empty;

    /// <summary>
    /// Gets all scripts that run after installation.
    /// </summary>
    public IReadOnlyDictionary<DepotId, RelativePath> InstallScripts { get; init; } = ImmutableDictionary<DepotId, RelativePath>.Empty;

    /// <summary>
    /// Gets all locally installed shared depots.
    /// </summary>
    /// <remarks>
    /// Shared depots are depots from another app and are commonly used for the Steamworks Common Redistributables.
    /// </remarks>
    public IReadOnlyDictionary<DepotId, AppId> SharedDepots { get; init; } = ImmutableDictionary<DepotId, AppId>.Empty;

    /// <summary>
    /// Gets the local user config.
    /// </summary>
    /// <remarks>
    /// This can contains keys like <c>language</c> or <c>BetaKey</c>.
    /// </remarks>
    /// <seealso cref="MountedConfig"/>
    public IReadOnlyDictionary<string, string> UserConfig { get; init; } = ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// Gets the local mounted config.
    /// </summary>
    /// <remarks>
    /// The meaning of these values are unknown. You'd think they have something to do with <see cref="UserConfig"/>
    /// but at the time of writing, I couldn't make out a clear connection since these values aren't being updated at all.
    /// </remarks>
    /// <seealso cref="UserConfig"/>
    public IReadOnlyDictionary<string, string> MountedConfig { get; init; } = ImmutableDictionary<string, string>.Empty;

    #endregion

    #region Helpers

    private static readonly RelativePath CommonDirectoryName = "common";
    private static readonly RelativePath ShaderCacheDirectoryName = "shadercache";
    private static readonly RelativePath WorkshopDirectoryName = "workshop";
    private static readonly RelativePath CompatabilityDataDirectoryName = "compatdata";

    /// <summary>
    /// Parses the file at <see cref="ManifestPath"/> again and returns a new
    /// instance of <see cref="AppManifest"/>.
    /// </summary>
    [Pure]
    [System.Diagnostics.Contracts.Pure]
    [MustUseReturnValue]
    public Result<AppManifest> Reload()
    {
        return AppManifestParser.ParseManifestFile(ManifestPath);
    }

    /// <summary>
    /// Gets the path to the <c>appworkshop_*.acf</c> file.
    /// </summary>
    /// <example><c>E:/SteamLibrary/steamapps/workshop/appworkshop_262060.acf</c></example>
    public AbsolutePath GetWorkshopManifestFilePath() => ManifestPath.Parent
        .Combine(WorkshopDirectoryName)
        .Combine($"appworkshop_{AppId.Value.ToString(CultureInfo.InvariantCulture)}.acf");

    /// <summary>
    /// Gets all locally installed DLCs.
    /// </summary>
    public IReadOnlyDictionary<AppId, InstalledDepot> GetInstalledDLCs() => InstalledDepots
        .Where(kv => kv.Value.DLCAppId != AppId.DefaultValue)
        .ToDictionary(kv => kv.Value.DLCAppId, kv => kv.Value);

    /// <summary>
    /// Gets the URL to the Update Notes for the current <see cref="BuildId"/> on SteamDB.
    /// </summary>
    public string GetCurrentUpdateNotesUrl() => BuildId.GetSteamDbUpdateNotesUrl();

    /// <summary>
    /// Gets the URL to the Update Notes for the next update using <see cref="TargetBuildId"/> on SteamDB.
    /// </summary>
    /// <remarks>
    /// This value will be <c>null</c>, if <see cref="TargetBuildId"/> is <see cref="ValueTypes.BuildId.DefaultValue"/>.
    /// </remarks>
    public string? GetNextUpdateNotesUrl() => TargetBuildId == BuildId.DefaultValue ? null : TargetBuildId.GetSteamDbUpdateNotesUrl();

    /// <summary>
    /// Gets the user-data path for the current app using <see cref="LastOwner"/> and
    /// <see cref="AppId"/>.
    /// </summary>
    /// <param name="steamDirectory">
    /// Path to the Steam installation directory. Example:
    /// <c>C:/Program Files/Steam</c>
    /// </param>
    /// <example><c>C:/Program Files/Steam/userdata/149956546/262060</c></example>
    /// <returns></returns>
    public AbsolutePath GetUserDataDirectoryPath(AbsolutePath steamDirectory)
    {
        return SteamLocationFinder.GetUserDataDirectoryPath(steamDirectory, LastOwner)
            .Combine(AppId.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Gets the path to the shader-cache directory.
    /// </summary>
    /// <example><c>E:/SteamLibrary/common/steamapps/shadercache/262060</c></example>
    public AbsolutePath GetShaderCacheDirectoryPath() => ManifestPath.Parent
        .Combine(ShaderCacheDirectoryName)
        .Combine(AppId.Value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Gets the path to the compatability data directory used by Proton.
    /// </summary>
    /// <example><c>/mnt/ssd/SteamLibrary/common/steamapps/compatdata/262060</c></example>
    public AbsolutePath GetCompatabilityDataDirectoryPath() => ManifestPath.Parent
        .Combine(CompatabilityDataDirectoryName)
        .Combine(AppId.Value.ToString(CultureInfo.InvariantCulture));

    #endregion

    #region Overwrites

    /// <inheritdoc/>
    public bool Equals(AppManifest? other)
    {
        if (other is null) return false;
        if (AppId != other.AppId) return false;
        if (Universe != other.Universe) return false;
        if (!string.Equals(Name, other.Name, StringComparison.Ordinal)) return false;
        if (StateFlags != other.StateFlags) return false;
        if (InstallationDirectory != other.InstallationDirectory) return false;
        if (LastUpdated != other.LastUpdated) return false;
        if (SizeOnDisk != other.SizeOnDisk) return false;
        if (StagingSize != other.StagingSize) return false;
        if (BuildId != other.BuildId) return false;
        if (LastOwner != other.LastOwner) return false;
        if (UpdateResult != other.UpdateResult) return false;
        if (BytesToDownload != other.BytesToDownload) return false;
        if (BytesDownloaded != other.BytesDownloaded) return false;
        if (BytesToStage != other.BytesToStage) return false;
        if (BytesStaged != other.BytesStaged) return false;
        if (TargetBuildId != other.TargetBuildId) return false;
        if (AutoUpdateBehavior != other.AutoUpdateBehavior) return false;
        if (BackgroundDownloadBehavior != other.BackgroundDownloadBehavior) return false;
        if (ScheduledAutoUpdate != other.ScheduledAutoUpdate) return false;
        if (FullValidateAfterNextUpdate != other.FullValidateAfterNextUpdate) return false;
        if (!InstalledDepots.SequenceEqual(other.InstalledDepots)) return false;
        if (!InstallScripts.SequenceEqual(other.InstallScripts)) return false;
        if (!SharedDepots.SequenceEqual(other.SharedDepots)) return false;
        if (!UserConfig.SequenceEqual(other.UserConfig)) return false;
        if (!MountedConfig.SequenceEqual(other.MountedConfig)) return false;
        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(ManifestPath);
        hashCode.Add(AppId);
        hashCode.Add((int)Universe);
        hashCode.Add(Name);
        hashCode.Add((int)StateFlags);
        hashCode.Add(InstallationDirectory);
        hashCode.Add(LastUpdated);
        hashCode.Add(SizeOnDisk);
        hashCode.Add(StagingSize);
        hashCode.Add(BuildId);
        hashCode.Add(LastOwner);
        hashCode.Add(UpdateResult);
        hashCode.Add(BytesToDownload);
        hashCode.Add(BytesDownloaded);
        hashCode.Add(BytesToStage);
        hashCode.Add(BytesStaged);
        hashCode.Add(TargetBuildId);
        hashCode.Add((int)AutoUpdateBehavior);
        hashCode.Add((int)BackgroundDownloadBehavior);
        hashCode.Add(ScheduledAutoUpdate);
        hashCode.Add(FullValidateAfterNextUpdate);
        hashCode.Add(InstalledDepots);
        hashCode.Add(InstallScripts);
        hashCode.Add(SharedDepots);
        hashCode.Add(UserConfig);
        hashCode.Add(MountedConfig);
        return hashCode.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.Append("{ ");
        sb.Append($"{nameof(AppId)} = {AppId}, ");
        sb.Append($"{nameof(Name)} = {Name}, ");
        sb.Append($"{nameof(InstallationDirectory)} = {InstallationDirectory}");
        sb.Append(" }");

        return sb.ToString();
    }

    #endregion
}
