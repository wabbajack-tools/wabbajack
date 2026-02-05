using System;
using System.Collections.Generic;
using System.IO;
using FluentResults;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;
using ValveKeyValue;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Services;

/// <summary>
/// Writer for <see cref="WorkshopManifest"/>.
/// </summary>
/// <seealso cref="WorkshopManifestParser"/>
[PublicAPI]
public class WorkshopManifestWriter
{
    /// <summary>
    /// Saves the manifest to file.
    /// </summary>
    public static Result Write(WorkshopManifest manifest, AbsolutePath outputPath)
    {
        var values = new List<KVObject>();
        values.AddValue("appid", manifest.AppId, AppId.DefaultValue);
        values.AddValue("SizeOnDisk", manifest.SizeOnDisk.Value, default);
        values.AddValue("NeedsUpdate", manifest.NeedsUpdate ? "1" : "0", string.Empty);
        values.AddValue("NeedsDownload", manifest.NeedsDownload ? "1" : "0", string.Empty);
        values.AddValue("TimeLastUpdated", manifest.LastUpdated.ToUnixTimeSeconds(), default);
        values.AddValue("TimeLastAppRan", manifest.LastAppStart.ToUnixTimeSeconds(), default);

        if (manifest.InstalledWorkshopItems.Count != 0)
        {
            var workshopItemsInstalledChildren = new List<KVObject>();
            var workshopItemDetailsChildren = new List<KVObject>();

            foreach (var kv in manifest.InstalledWorkshopItems)
            {
                var (workshopItemId, workshopItemDetails) = kv;

                var workshopItemInstalledValues = new List<KVObject>();
                workshopItemInstalledValues.AddValue("size", workshopItemDetails.SizeOnDisk.Value, default);
                workshopItemInstalledValues.AddValue("timeupdated", workshopItemDetails.LastUpdated.ToUnixTimeSeconds(), default);
                workshopItemInstalledValues.AddValue("manifest", workshopItemDetails.ManifestId, WorkshopManifestId.DefaultValue);

                var workshopItemDetailsValues = new List<KVObject>();
                workshopItemDetailsValues.AddValue("manifest", workshopItemDetails.ManifestId, WorkshopManifestId.DefaultValue);
                workshopItemDetailsValues.AddValue("timeupdated", workshopItemDetails.LastUpdated.ToUnixTimeSeconds(), default);
                workshopItemDetailsValues.AddValue("timetouched", workshopItemDetails.LastTouched.ToUnixTimeSeconds(), default);
                workshopItemDetailsValues.AddValue("subscribedby", workshopItemDetails.SubscribedBy.AccountId, default);

                workshopItemsInstalledChildren.Add(new KVObject(workshopItemId.ToString(), workshopItemInstalledValues));
                workshopItemDetailsChildren.Add(new KVObject(workshopItemId.ToString(), workshopItemDetailsValues));
            }

            values.Add(new KVObject("WorkshopItemsInstalled", workshopItemsInstalledChildren));
            values.Add(new KVObject("WorkshopItemDetails", workshopItemDetailsChildren));
        }

        var data = new KVObject("AppWorkshop", values);

        try
        {
            var serializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            using var stream = outputPath.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            serializer.Serialize(stream, data);
        }
        catch (Exception e)
        {
            return Result.Fail(
                new ExceptionalError("Exception while writing the manifest to file!", e)
                    .WithMetadata("AppId", manifest.AppId)
                    .WithMetadata("Path", outputPath)
            );
        }

        return Result.Ok();
    }
}
