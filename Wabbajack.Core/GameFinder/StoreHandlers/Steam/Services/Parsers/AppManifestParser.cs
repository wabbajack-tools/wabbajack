using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using FluentResults;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;
using Wabbajack.GameFinder.Paths.Utilities;
using ValveKeyValue;
using static Wabbajack.GameFinder.StoreHandlers.Steam.Services.ParserHelpers;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Services;

/// <summary>
/// Parser for <c>appmanifest_*.acf</c> files.
/// </summary>
/// <seealso cref="AppManifest"/>
[PublicAPI]
public static class AppManifestParser
{
    /// <summary>
    /// Parses the <c>appmanifest_*.acf</c> file at the given path.
    /// </summary>
    public static Result<AppManifest> ParseManifestFile(AbsolutePath manifestPath)
    {
        if (!manifestPath.FileExists)
        {
            return Result.Fail(new Error("Manifest file doesn't exist!")
                .WithMetadata("Path", manifestPath.GetFullPath())
            );
        }

        try
        {
            using var stream = manifestPath.Read();

            if (stream.Length == 0)
            {
                return Result.Fail(
                    new Error("Manifest file is empty!")
                        .WithMetadata("Path", manifestPath.GetFullPath())
                );
            }

            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var appState = kv.Deserialize(stream, KVSerializerOptions.DefaultOptions);

            if (appState is null)
            {
                return Result.Fail(
                    new Error($"{nameof(KVSerializer)} returned null trying to parse the manifest file!")
                        .WithMetadata("Path", manifestPath.GetFullPath())
                );
            }

            if (!appState.Name.Equals("AppState", StringComparison.Ordinal))
            {
                return Result.Fail(
                    new Error("Manifest file is potentially broken because the name doesn't match!")
                        .WithMetadata("Path", manifestPath.GetFullPath())
                        .WithMetadata("ExpectedName", "AppState")
                        .WithMetadata("ActualName", appState.Name)
                );
            }

            // NOTE (@erri120 on 2023-06-02):
            // The ValveKeyValue package by SteamDB (https://github.com/SteamDatabase/ValveKeyValue)
            // is currently "broken" and has multiple issues regarding parsing values
            // of type "uint", "ulong" and "string".
            // see the following links for more information:
            // - https://github.com/SteamDatabase/ValveKeyValue/pull/47
            // - https://github.com/SteamDatabase/ValveKeyValue/issues/53
            // - https://github.com/SteamDatabase/ValveKeyValue/issues/73
            // Until those issues are resolved or I find another library, parsing is going to be broken.

            var appIdResult = ParseRequiredChildObject(appState, "appid", ParseAppId);
            var universeResult = ParseOptionalChildObject(appState, "Universe", ParseUInt32, default).Map(x => (SteamUniverse)x);
            var nameResult = ParseRequiredChildObject(appState, "name", ParseString);
            var stateFlagsResult = ParseRequiredChildObject(appState, "StateFlags", ParseUInt32).Map(x => (StateFlags)x);
            var installationDirectoryNameResult = ParseInstallationDirectory(appState, manifestPath.FileSystem, manifestPath);
            var lastUpdatedResult = ParseOptionalChildObject(appState, "LastUpdated", ParseDateTimeOffset, DateTimeOffset.UnixEpoch);
            var sizeOnDiskResult = ParseOptionalChildObject(appState, "SizeOnDisk", ParseSize, Size.Zero);
            var stagingSizeResult = ParseOptionalChildObject(appState, "StagingSize", ParseSize, Size.Zero);
            var buildIdResult = ParseOptionalChildObject(appState, "buildid", ParseBuildId, BuildId.DefaultValue);
            var lastOwnerResult = ParseOptionalChildObject(appState, "LastOwner", ParseSteamId, SteamId.Empty);
            var updateResult = ParseOptionalChildObject(appState, "UpdateResult", ParseUInt32, default);
            var bytesToDownloadResult = ParseOptionalChildObject(appState, "BytesToDownload", ParseSize, Size.Zero);
            var bytesDownloadedResult = ParseOptionalChildObject(appState, "BytesDownloaded", ParseSize, Size.Zero);
            var bytesToStageResult = ParseOptionalChildObject(appState, "BytesToStage", ParseSize, Size.Zero);
            var bytesStagedResult = ParseOptionalChildObject(appState, "BytesStaged", ParseSize, Size.Zero);
            var targetBuildIdResult = ParseOptionalChildObject(appState, "TargetBuildID", ParseBuildId, BuildId.DefaultValue);
            var autoUpdateBehaviorResult = ParseOptionalChildObject(appState, "AutoUpdateBehavior", ParseByte, default).Map(x => (AutoUpdateBehavior)x);
            var backgroundDownloadBehaviorResult = ParseOptionalChildObject(appState, "AllowOtherDownloadsWhileRunning", ParseByte, default).Map(x => (BackgroundDownloadBehavior)x);
            var scheduledAutoUpdateResult = ParseOptionalChildObject(appState, "ScheduledAutoUpdate", ParseDateTimeOffset, DateTimeOffset.UnixEpoch);
            var fullValidateAfterNextUpdateResult = ParseOptionalChildObject(appState, "FullValidateAfterNextUpdate", ParseBool, default);

            var installedDepotsResult = ParseInstalledDepots(appState);
            var installScriptsResult = ParseBasicDictionary(
                appState,
                "InstallScripts",
                key => DepotId.From(uint.Parse(key)),
                x => ParseRelativePath(x));

            var sharedDepotsResult = ParseBasicDictionary(
                appState,
                "SharedDepots",
                key => DepotId.From(uint.Parse(key)),
                ParseAppId);

            var userConfigResult = ParseBasicDictionary(
                appState,
                "UserConfig",
                key => key,
                ParseString,
                StringComparer.OrdinalIgnoreCase);

            var mountedConfigResult = ParseBasicDictionary(
                appState,
                "MountedConfig",
                key => key,
                ParseString,
                StringComparer.OrdinalIgnoreCase);

            var mergedResults = Result.Merge(
                appIdResult,
                universeResult,
                nameResult,
                stateFlagsResult,
                installationDirectoryNameResult,
                lastUpdatedResult,
                sizeOnDiskResult,
                stagingSizeResult,
                buildIdResult,
                lastOwnerResult,
                updateResult,
                bytesToDownloadResult,
                bytesDownloadedResult,
                bytesToStageResult,
                bytesStagedResult,
                targetBuildIdResult,
                autoUpdateBehaviorResult,
                backgroundDownloadBehaviorResult,
                scheduledAutoUpdateResult,
                fullValidateAfterNextUpdateResult,
                installedDepotsResult,
                installScriptsResult,
                sharedDepotsResult,
                userConfigResult,
                mountedConfigResult
            );

            if (mergedResults.IsFailed) return mergedResults;

            return Result.Ok(
                new AppManifest
                {
                    ManifestPath = manifestPath,
                    AppId = appIdResult.Value,
                    Universe = universeResult.Value,
                    Name = nameResult.Value,
                    StateFlags = stateFlagsResult.Value,
                    InstallationDirectory = installationDirectoryNameResult.Value,

                    LastUpdated = lastUpdatedResult.Value,
                    SizeOnDisk = sizeOnDiskResult.Value,
                    StagingSize = stagingSizeResult.Value,
                    BuildId = buildIdResult.Value,
                    LastOwner = lastOwnerResult.Value,
                    UpdateResult = updateResult.Value,
                    BytesToDownload = bytesToDownloadResult.Value,
                    BytesDownloaded = bytesDownloadedResult.Value,
                    BytesToStage = bytesToStageResult.Value,
                    BytesStaged = bytesStagedResult.Value,
                    TargetBuildId = targetBuildIdResult.Value,
                    AutoUpdateBehavior = autoUpdateBehaviorResult.Value,
                    BackgroundDownloadBehavior = backgroundDownloadBehaviorResult.Value,
                    ScheduledAutoUpdate = scheduledAutoUpdateResult.Value,
                    FullValidateAfterNextUpdate = fullValidateAfterNextUpdateResult.Value,

                    InstalledDepots = installedDepotsResult.Value,
                    InstallScripts = installScriptsResult.Value,
                    SharedDepots = sharedDepotsResult.Value,
                    UserConfig = userConfigResult.Value,
                    MountedConfig = mountedConfigResult.Value,
                }
            );
        }
        catch (Exception ex)
        {
            return Result.Fail(
                new ExceptionalError("Exception was thrown while parsing the manifest file!", ex)
                    .WithMetadata("Path", manifestPath.GetFullPath())
            );
        }
    }

    private static Result<AbsolutePath> ParseInstallationDirectory(KVObject appState, IFileSystem fileSystem, AbsolutePath manifestPath)
    {
        var installDirectoryResult = FindRequiredChildObject(appState, "installdir");
        if (installDirectoryResult.IsFailed) return installDirectoryResult.ToResult();

        var parseResult = ParseChildObjectValue(installDirectoryResult.Value, appState, ParseString);
        if (parseResult.IsFailed) return parseResult.ToResult();

        var rawPath = parseResult.Value;
        var sanitizedPath = PathHelpers.Sanitize(rawPath);
        var isRelative = !PathHelpers.IsRooted(sanitizedPath);

        if (isRelative)
        {
            var relativePath = RelativePath.CreateUnsafe(sanitizedPath);
            return Result.Ok(manifestPath.Parent.Combine("common").Combine(relativePath));
        }

        var absolutePath = fileSystem.FromUnsanitizedFullPath(sanitizedPath);
        return absolutePath;
    }

    private static Result<IReadOnlyDictionary<DepotId, InstalledDepot>> ParseInstalledDepots(KVObject appState)
    {
        var installedDepotsObject = FindOptionalChildObject(appState, "InstalledDepots");
        if (installedDepotsObject is null)
        {
            return Result.Ok(
                (IReadOnlyDictionary<DepotId, InstalledDepot>)ImmutableDictionary<DepotId, InstalledDepot>.Empty
            );
        }

        var installedDepotResults = installedDepotsObject.Children
            .Select(ParseInstalledDepot)
            .ToArray();

        var mergedResults = Result.Merge(installedDepotResults);
        return mergedResults.Bind(installedDepots =>
            Result.Ok(
                (IReadOnlyDictionary<DepotId, InstalledDepot>)installedDepots
                    .ToDictionary(x => x.DepotId, x => x)
            )
        );
    }

    private static Result<InstalledDepot> ParseInstalledDepot(KVObject depotObject)
    {
        if (!uint.TryParse(depotObject.Name, NumberFormatInfo.InvariantInfo, out var rawDepotId))
        {
            return Result.Fail(
                new Error("Unable to parse Depot name as a 32-bit unsigned integer!")
                    .WithMetadata("OriginalName", depotObject.Name)
            );
        }

        var depotId = DepotId.From(rawDepotId);

        var manifestIdResult = ParseRequiredChildObject(depotObject, "manifest", ParseManifestId);
        var sizeOnDiskResult = ParseRequiredChildObject(depotObject, "size", ParseSize);
        var dlcAppIdResult = ParseOptionalChildObject(depotObject, "dlcappid", ParseAppId, AppId.DefaultValue);

        var mergedResults = Result.Merge(
            manifestIdResult,
            sizeOnDiskResult,
            dlcAppIdResult
        );

        if (mergedResults.IsFailed) return mergedResults;

        var installedDepot = new InstalledDepot
        {
            DepotId = depotId,
            ManifestId = manifestIdResult.Value,
            SizeOnDisk = sizeOnDiskResult.Value,
            DLCAppId = dlcAppIdResult.Value,
        };

        return Result.Ok(installedDepot);
    }
}
