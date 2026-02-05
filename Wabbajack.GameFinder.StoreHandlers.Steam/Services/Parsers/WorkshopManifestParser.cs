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
using ValveKeyValue;
using static Wabbajack.GameFinder.StoreHandlers.Steam.Services.ParserHelpers;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Services;

/// <summary>
/// Parser for <c>appworkshop_*.acf</c> files.
/// </summary>
/// <seealso cref="WorkshopManifest"/>
[PublicAPI]
public static class WorkshopManifestParser
{
    /// <summary>
    /// Parses the <c>appworkshop_*.acf</c> file at the given path.
    /// </summary>
    public static Result<WorkshopManifest> ParseManifestFile(AbsolutePath manifestPath)
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

            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var appWorkshop = kv.Deserialize(stream, KVSerializerOptions.DefaultOptions);

            if (appWorkshop is null)
            {
                return Result.Fail(
                    new Error($"{nameof(KVSerializer)} returned null trying to parse the manifest file!")
                        .WithMetadata("Path", manifestPath.GetFullPath())
                );
            }

            if (!appWorkshop.Name.Equals("AppWorkshop", StringComparison.Ordinal))
            {
                return Result.Fail(
                    new Error("Manifest file is potentially broken because the name doesn't match!")
                        .WithMetadata("Path", manifestPath.GetFullPath())
                        .WithMetadata("ExpectedName", "AppWorkshop")
                        .WithMetadata("ActualName", appWorkshop.Name)
                );
            }

            var appIdResult = ParseRequiredChildObject(appWorkshop, "appid", ParseAppId);
            var sizeOnDiskResult = ParseOptionalChildObject(appWorkshop, "SizeOnDisk", ParseSize, Size.Zero);
            var needsUpdateResult = ParseOptionalChildObject(appWorkshop, "NeedsUpdate", ParseBool, default);
            var needsDownloadResult = ParseOptionalChildObject(appWorkshop, "NeedsDownload", ParseBool, default);
            var lastUpdatedResult = ParseOptionalChildObject(appWorkshop, "TimeLastUpdated", ParseDateTimeOffset, DateTimeOffset.UnixEpoch);
            var lastAppStartResult = ParseOptionalChildObject(appWorkshop, "TimeLastAppRan", ParseDateTimeOffset, DateTimeOffset.UnixEpoch);

            var installedWorkshopItems = ParseInstalledWorkshopItems(appWorkshop);

            var mergedResults = Result.Merge(
                appIdResult,
                sizeOnDiskResult,
                needsUpdateResult,
                needsDownloadResult,
                lastUpdatedResult,
                lastAppStartResult,
                installedWorkshopItems
            );

            if (mergedResults.IsFailed) return mergedResults;

            return Result.Ok(
                new WorkshopManifest
                {
                    ManifestPath = manifestPath,
                    AppId = appIdResult.Value,
                    SizeOnDisk = sizeOnDiskResult.Value,
                    NeedsUpdate = needsUpdateResult.Value,
                    NeedsDownload = needsDownloadResult.Value,
                    LastUpdated = lastUpdatedResult.Value,
                    LastAppStart = lastAppStartResult.Value,
                    InstalledWorkshopItems = installedWorkshopItems.Value,
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

    private static Result<IReadOnlyDictionary<WorkshopItemId, WorkshopItemDetails>> ParseInstalledWorkshopItems(KVObject appWorkshop)
    {
        var installedWorkshopItemsObject = FindOptionalChildObject(appWorkshop, "WorkshopItemsInstalled");
        if (installedWorkshopItemsObject is null)
        {
            return Result.Ok(
                (IReadOnlyDictionary<WorkshopItemId, WorkshopItemDetails>)ImmutableDictionary<WorkshopItemId, WorkshopItemDetails>.Empty
            );
        }

        var installedWorkshopItemResults = installedWorkshopItemsObject.Children
            .Select(ParseInstalledWorkshopItem)
            .ToArray();

        var mergedResults = Result.Merge(installedWorkshopItemResults);
        if (mergedResults.IsFailed) return mergedResults.ToResult();

        var workshopItemDetailsObject = FindOptionalChildObject(appWorkshop, "WorkshopItemDetails");
        if (workshopItemDetailsObject is null)
        {
            return Result.Ok(
                (IReadOnlyDictionary<WorkshopItemId, WorkshopItemDetails>)mergedResults.Value.ToDictionary(x => x.ItemId, x => x)
            );
        }

        var installedWorkshopItems = mergedResults.Value!.ToList();
        installedWorkshopItemResults = workshopItemDetailsObject.Children
            .Select(workshopItemDetailObject => ParseWorkshopItemDetails(workshopItemDetailObject, installedWorkshopItems))
            .ToArray();

        mergedResults = Result.Merge(installedWorkshopItemResults);
        if (mergedResults.IsFailed) return mergedResults.ToResult();

        return Result.Ok(
            (IReadOnlyDictionary<WorkshopItemId, WorkshopItemDetails>)mergedResults.Value.ToDictionary(x => x.ItemId, x => x)
        );
    }

    private static Result<WorkshopItemDetails> ParseInstalledWorkshopItem(KVObject installedWorkshopItemObject)
    {
        if (!ulong.TryParse(installedWorkshopItemObject.Name, NumberFormatInfo.InvariantInfo, out var rawWorkshopItemId))
        {
            return Result.Fail(
                new Error("Unable to parse WorkshopItem name as a 64-bit unsigned integer!")
                    .WithMetadata("OriginalName", installedWorkshopItemObject.Name)
            );
        }

        var workshopItemId = WorkshopItemId.From(rawWorkshopItemId);

        var sizeOnDiskResult = ParseRequiredChildObject(installedWorkshopItemObject, "size", ParseSize);
        var lastUpdatedResult = ParseOptionalChildObject(installedWorkshopItemObject, "timeupdated", ParseDateTimeOffset, DateTimeOffset.UnixEpoch);
        var manifestResult = ParseRequiredChildObject(installedWorkshopItemObject, "manifest", ParseWorkshopManifestId);

        var mergedResults = Result.Merge(
            sizeOnDiskResult,
            lastUpdatedResult,
            manifestResult
        );

        if (mergedResults.IsFailed) return mergedResults;

        return Result.Ok(
            new WorkshopItemDetails
            {
                ItemId = workshopItemId,
                SizeOnDisk = sizeOnDiskResult.Value,
                ManifestId = manifestResult.Value,
                LastUpdated = lastUpdatedResult.Value,
            }
        );
    }

    private static Result<WorkshopItemDetails> ParseWorkshopItemDetails(
        KVObject workshopItemDetailObject,
        IEnumerable<WorkshopItemDetails> installedWorkshopItems)
    {
        if (!ulong.TryParse(workshopItemDetailObject.Name, NumberFormatInfo.InvariantInfo, out var rawWorkshopItemId))
        {
            return Result.Fail(
                new Error("Unable to parse WorkshopItem name as a 64-bit unsigned integer!")
                    .WithMetadata("OriginalName", workshopItemDetailObject.Name)
            );
        }

        var workshopItemId = WorkshopItemId.From(rawWorkshopItemId);

        var manifestResult = ParseRequiredChildObject(workshopItemDetailObject, "manifest", ParseWorkshopManifestId);
        var lastUpdatedResult = ParseOptionalChildObject(workshopItemDetailObject, "timeupdated", ParseDateTimeOffset, DateTimeOffset.UnixEpoch);
        var lastTouchedResult = ParseOptionalChildObject(workshopItemDetailObject, "timetouched", ParseDateTimeOffset, DateTimeOffset.UnixEpoch);
        var subscribedByResult = ParseOptionalChildObject(workshopItemDetailObject, "subscribedby", ParseUInt32, default).Map(x => SteamId.FromAccountId(x));

        var mergedResults = Result.Merge(
            manifestResult,
            lastUpdatedResult,
            lastTouchedResult,
            subscribedByResult
        );

        if (mergedResults.IsFailed) return mergedResults;

        var installedWorkshopItem = installedWorkshopItems.FirstOrDefault(x => x.ItemId == workshopItemId);
        if (installedWorkshopItem is null)
        {
            return Result.Fail(
                new Error("Unable to find previously parsed installed workshop item!")
                    .WithMetadata("WorkshopItemId", workshopItemId)
            );
        }

        return Result.Ok(
            installedWorkshopItem with
            {
                ManifestId = manifestResult.Value,
                LastUpdated = lastUpdatedResult.Value,
                LastTouched = lastTouchedResult.Value,
                SubscribedBy = subscribedByResult.Value
            }
        );
    }
}
