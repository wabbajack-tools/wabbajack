using System;
using System.Collections.Generic;
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
/// Parser for <see cref="LocalUserConfig"/>
/// </summary>
/// <seealso cref="LocalUserConfigWriter"/>
[PublicAPI]
public static class LocalUserConfigParser
{
    /// <summary>
    /// Parses the local user config file.
    /// </summary>
    public static Result<LocalUserConfig> ParseConfigFile(SteamId steamId, AbsolutePath configPath)
    {
        if (!configPath.FileExists)
        {
            return Result.Fail(new Error("Config file doesn't exist!")
                .WithMetadata("Path", configPath.GetFullPath())
            );
        }

        try
        {
            using var stream = configPath.Read();

            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var localConfigStore = kv.Deserialize(stream, new KVSerializerOptions
            {
                HasEscapeSequences = true,
            });

            if (localConfigStore is null)
            {
                return Result.Fail(
                    new Error($"{nameof(KVSerializer)} returned null trying to parse the config file!")
                        .WithMetadata("Path", configPath.GetFullPath())
                );
            }

            if (!localConfigStore.Name.Equals("UserLocalConfigStore", StringComparison.Ordinal))
            {
                return Result.Fail(
                    new Error("Config file is potentially broken because the name doesn't match!")
                        .WithMetadata("Path", configPath.GetFullPath())
                        .WithMetadata("ExpectedName", "UserLocalConfigStore")
                        .WithMetadata("ActualName", localConfigStore.Name)
                );
            }

            var localAppDataResult = ParseLocalAppData(localConfigStore);

            var webStorageObject = FindOptionalChildObject(localConfigStore, "WebStorage");
            var systemObject = FindOptionalChildObject(webStorageObject, "system");
            var inGameOverlayScreenshotSaveUncompressedPathResult = systemObject is null
                ? Result.Ok<AbsolutePath?>(value: null)
                : ParseOptionalChildObject(
                    systemObject,
                    "InGameOverlayScreenshotSaveUncompressedPath",
                    x => (AbsolutePath?)ParseAbsolutePath(x, configPath.FileSystem),
                    defaultValue: null
                );

            var mergedResults = Result.Merge(
                localAppDataResult,
                inGameOverlayScreenshotSaveUncompressedPathResult
            );

            if (mergedResults.IsFailed) return mergedResults;

            return Result.Ok(
                new LocalUserConfig
                {
                    ConfigPath = configPath,
                    User = steamId,
                    LocalAppData = localAppDataResult.Value,
                    InGameOverlayScreenshotSaveUncompressedPath = inGameOverlayScreenshotSaveUncompressedPathResult.Value,
                }
            );
        }
        catch (Exception ex)
        {
            return Result.Fail(
                new ExceptionalError("Exception was thrown while parsing the config file!", ex)
                    .WithMetadata("Path", configPath.GetFullPath())
            );
        }
    }

    private static Result<IReadOnlyDictionary<AppId, LocalAppData>> ParseLocalAppData(KVObject localConfigStore)
    {
        var softwareResult = FindRequiredChildObject(localConfigStore, "Software");
        if (softwareResult.IsFailed) return softwareResult.ToResult();

        var valveResult = FindRequiredChildObject(softwareResult.Value, "Valve");
        if (valveResult.IsFailed) return valveResult.ToResult();

        var steamResult = FindRequiredChildObject(valveResult.Value, "Steam");
        if (steamResult.IsFailed) return steamResult.ToResult();

        var appsResult = FindRequiredChildObject(steamResult.Value, "apps");
        if (appsResult.IsFailed) return appsResult.ToResult();

        var appResults = appsResult.Value.Children
            .Select(ParseSingleLocalAppData)
            .ToArray();

        var mergedResults = Result.Merge(appResults);
        return mergedResults.Bind(appData =>
            Result.Ok(
                (IReadOnlyDictionary<AppId, LocalAppData>)appData
                    .ToDictionary(x => x.AppId, x => x)
            )
        );
    }

    private static Result<LocalAppData> ParseSingleLocalAppData(KVObject appObject)
    {
        if (!uint.TryParse(appObject.Name, NumberFormatInfo.InvariantInfo, out var rawAppId))
        {
            return Result.Fail(
                new Error("Unable to parse AppId as a 32-bit unsigned integer!")
                    .WithMetadata("OriginalName", appObject.Name)
            );
        }

        var appId = AppId.From(rawAppId);

        var lastPlayedResult = ParseOptionalChildObject(appObject, "LastPlayed", ParseDateTimeOffset, DateTimeOffset.UnixEpoch);
        var playtimeResult = ParseOptionalChildObject(appObject, "Playtime", ParseUInt32, default).Map(x => TimeSpan.FromMinutes(x));
        var launchOptionsResult = ParseOptionalChildObject(appObject, "LaunchOptions", ParseString, string.Empty);

        var mergedResults = Result.Merge(
            lastPlayedResult,
            playtimeResult,
            launchOptionsResult
        );

        if (mergedResults.IsFailed) return mergedResults;

        var localAppData = new LocalAppData
        {
            AppId = appId,
            LastPlayed = lastPlayedResult.Value,
            Playtime = playtimeResult.Value,
            LaunchOptions = launchOptionsResult.Value,
        };

        return Result.Ok(localAppData);
    }
}
