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
/// Writer for <see cref="AppManifest"/>.
/// </summary>
/// <seealso cref="AppManifestParser"/>
[PublicAPI]
public static class AppManifestWriter
{
    /// <summary>
    /// Saves the manifest to file.
    /// </summary>
    public static Result Write(AppManifest manifest, AbsolutePath outputPath)
    {
        var values = new List<KVObject>();
        values.AddValue("appid", manifest.AppId, AppId.DefaultValue);
        values.AddValue("Universe", (byte)manifest.Universe, -1);
        values.AddValue("name", manifest.Name, string.Empty);
        values.AddValue("StateFlags", (byte)manifest.StateFlags, -1);
        values.AddValue("installdir", manifest.InstallationDirectory.Name.ToString(), string.Empty);
        values.AddValue("LastUpdated", manifest.LastUpdated.ToUnixTimeSeconds(), default);
        values.AddValue("SizeOnDisk", manifest.SizeOnDisk.Value, default);
        values.AddValue("StagingSize", manifest.StagingSize.Value, default);
        values.AddValue("buildid", manifest.BuildId, BuildId.DefaultValue);
        values.AddValue("LastOwner", manifest.LastOwner.RawId, SteamId.Empty.RawId);
        values.AddValue("UpdateResult", manifest.UpdateResult, default);
        values.AddValue("BytesToDownload", manifest.BytesToDownload.Value, default);
        values.AddValue("BytesDownloaded", manifest.BytesDownloaded.Value, default);
        values.AddValue("BytesToStage", manifest.BytesToStage.Value, default);
        values.AddValue("BytesStaged", manifest.BytesStaged.Value, default);
        values.AddValue("TargetBuildID", manifest.TargetBuildId, BuildId.DefaultValue);
        values.AddValue("AutoUpdateBehavior", (byte)manifest.AutoUpdateBehavior, -1);
        values.AddValue("AllowOtherDownloadsWhileRunning", (byte)manifest.BackgroundDownloadBehavior, -1);
        values.AddValue("ScheduledAutoUpdate", manifest.ScheduledAutoUpdate.ToUnixTimeSeconds(), default);
        values.AddValue("FullValidateAfterNextUpdate", manifest.FullValidateAfterNextUpdate ? "1" : "0", string.Empty);

        if (manifest.InstalledDepots.Count != 0)
        {
            var children = new List<KVObject>();

            foreach (var kv in manifest.InstalledDepots)
            {
                var (depotId, installedDepot) = kv;
                var objValues = new List<KVObject>();
                objValues.AddValue("manifest", installedDepot.ManifestId, ManifestId.DefaultValue);
                objValues.AddValue("size", installedDepot.SizeOnDisk.Value, default);
                objValues.AddValue("dlcappid", installedDepot.DLCAppId, AppId.DefaultValue);

                var obj = new KVObject(depotId.ToString(), objValues);
                children.Add(obj);
            }

            values.Add(new KVObject("InstalledDepots", children));
        }

        values.AddDictionary("InstallScripts", manifest.InstallScripts, RelativePath.Empty);
        values.AddDictionary("SharedDepots", manifest.SharedDepots, AppId.DefaultValue);
        values.AddDictionary("UserConfig", manifest.UserConfig, string.Empty);
        values.AddDictionary("MountedConfig", manifest.MountedConfig, string.Empty);

        var data = new KVObject("AppState", values);

        try
        {
            var serializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            using var stream = outputPath.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            serializer.Serialize(stream, data);
        }
        catch (Exception e)
        {
            return Result.Fail(
                new ExceptionalError("Exception while writing the AppManifest to file!", e)
                    .WithMetadata("AppId", manifest.AppId)
                    .WithMetadata("Path", outputPath)
            );
        }

        return Result.Ok();
    }
}
