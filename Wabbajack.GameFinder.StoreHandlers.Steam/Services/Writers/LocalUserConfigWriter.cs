using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FluentResults;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;
using ValveKeyValue;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Services;

/// <summary>
/// Writer for <see cref="LocalUserConfig"/>.
/// </summary>
/// <seealso cref="LocalUserConfigParser"/>
[PublicAPI]
public static class LocalUserConfigWriter
{
    /// <summary>
    /// Saves the local user config to file.
    /// </summary>
    public static Result Write(LocalUserConfig config, AbsolutePath outputPath)
    {
        var values = new List<KVObject>();

        var appDataObjects = config.LocalAppData.Select(x =>
        {
            var (appId, data) = x;

            var innerValues = new List<KVObject>();
            innerValues.AddValue("LastPlayed", data.LastPlayed.ToUnixTimeSeconds(), default);
            innerValues.AddValue("Playtime", (int)data.Playtime.TotalMinutes, default);
            innerValues.AddValue("LaunchOptions", data.LaunchOptions, string.Empty);

            return new KVObject(appId.Value.ToString(CultureInfo.InvariantCulture), innerValues);
        });

        values.Add(new KVObject("Software", new[]
        {
            new KVObject("Valve", new []
            {
                new KVObject("Steam", new []
                {
                    new KVObject("apps", appDataObjects),
                }),
            }),
        }));

        var systemObjects = new List<KVObject>();
        systemObjects.AddValue("InGameOverlayScreenshotSaveUncompressedPath", config.InGameOverlayScreenshotSaveUncompressedPath?.ToString() ?? string.Empty, string.Empty);

        values.Add(new KVObject("WebStorage", new[]
        {
            new KVObject("system", systemObjects),
        }));

        var data = new KVObject("UserLocalConfigStore", values);

        try
        {
            var serializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            using var stream = outputPath.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            serializer.Serialize(stream, data);
        }
        catch (Exception e)
        {
            return Result.Fail(
                new ExceptionalError("Exception while writing the local user config to file!", e)
                    .WithMetadata("Path", outputPath)
            );
        }

        return Result.Ok();
    }
}
