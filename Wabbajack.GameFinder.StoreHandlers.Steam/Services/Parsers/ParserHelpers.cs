using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentResults;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using Wabbajack.GameFinder.Paths;
using Wabbajack.GameFinder.Paths.Utilities;
using ValveKeyValue;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Services;

internal static class ParserHelpers
{
    #region Core Parsers

    internal static Result<IReadOnlyDictionary<TKey, TValue>> ParseBasicDictionary<TKey, TValue>(
        KVObject parentObject,
        string dictionaryObjectName,
        Func<string, TKey> keyParser,
        Func<KVValue, TValue> valueParser,
        IEqualityComparer<TKey>? equalityComparer = null)
        where TKey : notnull
    {
        var dictionaryObject = FindOptionalChildObject(parentObject, dictionaryObjectName);
        if (dictionaryObject is null)
        {
            return Result.Ok(
                (IReadOnlyDictionary<TKey, TValue>)ImmutableDictionary<TKey, TValue>.Empty
            );
        }

        var dictionaryValueResults = dictionaryObject.Children
            .Select<KVObject, Result<KeyValuePair<TKey, TValue>>>(childObject =>
            {
                var keyResult = Result.Try(() => keyParser(childObject.Name));
                var valueResult = ParseValue(childObject.Value, valueParser);

                var mergedResult = Result.Merge(
                    keyResult,
                    valueResult
                );

                if (mergedResult.IsFailed) return mergedResult;
                return Result.Ok(new KeyValuePair<TKey, TValue>(keyResult.Value, valueResult.Value));
            }).ToArray();

        var mergedResults = Result.Merge(dictionaryValueResults);
        return mergedResults.Bind(values => Result.Ok(
                (IReadOnlyDictionary<TKey, TValue>)values.ToDictionary(x => x.Key, x => x.Value, equalityComparer)
            )
        );
    }

    private static Result<T> ParseValue<T>(KVValue value, Func<KVValue, T> parser)
    {
        return Result.Try(
            () => parser(value),
            ex => new ExceptionalError("Unable to parse value!", ex)
        );
    }

    internal static Result<T> ParseChildObjectValue<T>(
        KVObject childObject,
        KVObject parentObject,
        Func<KVValue, T> parser)
    {
        return Result.Try(
            () => parser(childObject.Value),
            ex => new ExceptionalError("Unable to parse value of child object!", ex)
                .WithMetadata("ChildObjectName", childObject.Name)
                .WithMetadata("ParentObjectName", parentObject.Name)
        );
    }

    internal static KVObject? FindOptionalChildObject(KVObject? parentObject, string childObjectName)
    {
        if (parentObject is null) return null;

        var childObject = parentObject
            .Children
            .FirstOrDefault(child => child.Name.Equals(childObjectName, StringComparison.OrdinalIgnoreCase));

        if (childObject is null && Debugger.IsLogging())
        {
            Debugger.Log(0, Debugger.DefaultCategory, $"Optional child object {childObjectName} was not found in {parentObject.Name}");
        }

        return childObject;
    }

    internal static Result<T> ParseOptionalChildObject<T>(
        KVObject parentObject,
        string childObjectName,
        Func<KVValue, T> parser,
        T defaultValue)
    {
        var childObject = FindOptionalChildObject(parentObject, childObjectName);
        return childObject is null
            ? Result.Ok(defaultValue)
            : ParseChildObjectValue(childObject, parentObject, parser);
    }

    internal static Result<KVObject> FindRequiredChildObject(KVObject parentObject, string childObjectName)
    {
        var childObject = FindOptionalChildObject(parentObject, childObjectName);

        if (childObject is null)
        {
            return Result.Fail(
                new Error("Unable to find required child object by name in parent!")
                    .WithMetadata("ChildObjectName", childObjectName)
                    .WithMetadata("ParentObjectName", parentObject.Name)
            );
        }

        return Result.Ok(childObject);
    }

    internal static Result<T> ParseRequiredChildObject<T>(
        KVObject parentObject,
        string childObjectName,
        Func<KVValue, T> parser)
    {
        var childObjectResult = FindRequiredChildObject(parentObject, childObjectName);
        return childObjectResult.Bind(childObject => ParseChildObjectValue(childObject, parentObject, parser));
    }

    #endregion

    #region Type Parser

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte ParseByte(KVValue value) => byte.Parse(ParseString(value), CultureInfo.InvariantCulture);

    internal static bool ParseBool(KVValue value)
    {
        var s = ParseString(value);
        if (string.Equals(s, "0", StringComparison.Ordinal)) return false;
        if (string.Equals(s, "1", StringComparison.Ordinal)) return true;
        throw new FormatException($"Unable to parse '{value}' as a boolean!");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ParseUInt32(KVValue value) => uint.Parse(ParseString(value), CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ParseUInt64(KVValue value) => ulong.Parse(ParseString(value), CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ParseString(KVValue value) => value.ToString(CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static DateTimeOffset ParseDateTimeOffset(KVValue value) => DateTimeOffset.FromUnixTimeSeconds(ParseUInt32(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SteamId ParseSteamId(KVValue value) => SteamId.From(ParseUInt64(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static AppId ParseAppId(KVValue value) => AppId.From(ParseUInt32(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static BuildId ParseBuildId(KVValue value) => BuildId.From(ParseUInt32(value));

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // internal static DepotId ParseDepotId(KVValue value) => DepotId.From(ParseUInt32(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ManifestId ParseManifestId(KVValue value) => ManifestId.From(ParseUInt64(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static WorkshopManifestId ParseWorkshopManifestId(KVValue value) => WorkshopManifestId.From(ParseUInt64(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Size ParseSize(KVValue value) => Size.From(ParseUInt64(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RelativePath ParseRelativePath(KVValue value) => ParseString(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static AbsolutePath ParseAbsolutePath(KVValue value, IFileSystem fileSystem) => fileSystem.FromUnsanitizedFullPath(ParseString(value));

    #endregion
}
