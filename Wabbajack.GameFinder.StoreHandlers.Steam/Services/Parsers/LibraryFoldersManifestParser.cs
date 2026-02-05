using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentResults;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models;
using Wabbajack.GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wabbajack.GameFinder.Paths;
using ValveKeyValue;
using static Wabbajack.GameFinder.StoreHandlers.Steam.Services.ParserHelpers;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Services;

/// <summary>
/// Parser for <c>libraryfolders.vdf</c> files.
/// </summary>
/// <seealso cref="LibraryFoldersManifest"/>
[PublicAPI]
public static class LibraryFoldersManifestParser
{
    /// <summary>
    /// Parses the <c>libraryfolders.vdf</c> file at the given path.
    /// </summary>
    public static Result<LibraryFoldersManifest> ParseManifestFile(AbsolutePath manifestPath)
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
            var data = kv.Deserialize(stream, KVSerializerOptions.DefaultOptions);

            if (data is null)
            {
                return Result.Fail(
                    new Error($"{nameof(KVSerializer)} returned null trying to parse the manifest file!")
                        .WithMetadata("Path", manifestPath.GetFullPath())
                );
            }

            if (!data.Name.Equals("libraryfolders", StringComparison.Ordinal))
            {
                return Result.Fail(
                    new Error("Manifest file is potentially broken because the name doesn't match!")
                        .WithMetadata("Path", manifestPath.GetFullPath())
                        .WithMetadata("ExpectedName", "libraryfolders")
                        .WithMetadata("ActualName", data.Name)
                );
            }

            var libraryFoldersResult = ParseLibraryFolders(data, manifestPath.FileSystem);
            if (libraryFoldersResult.IsFailed) return libraryFoldersResult.ToResult();

            return Result.Ok(
                new LibraryFoldersManifest
                {
                    ManifestPath = manifestPath,
                    LibraryFolders = libraryFoldersResult.Value,
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

    private static Result<IReadOnlyList<LibraryFolder>> ParseLibraryFolders(KVObject data, IFileSystem fileSystem)
    {
        var libraryFolderResults = data.Children
            .Select(c => ParseLibraryFolder(c, fileSystem))
            .ToArray();

        return Result.Merge(libraryFolderResults).Bind(e => Result.Ok((IReadOnlyList<LibraryFolder>)e.ToList()));
    }

    private static Result<LibraryFolder> ParseLibraryFolder(KVObject parent, IFileSystem fileSystem)
    {
        var pathResult = ParseRequiredChildObject(parent, "path", value => ParseAbsolutePath(value, fileSystem));
        var labelResult = ParseOptionalChildObject(parent, "label", ParseString, string.Empty);
        var totalSizeResult = ParseOptionalChildObject(parent, "totalsize", ParseSize, Size.Zero);

        var appSizesResult = ParseBasicDictionary(
            parent,
            "apps",
            key => AppId.From(uint.Parse(key)),
            ParseSize);

        var mergedResults = Result.Merge(
            pathResult,
            labelResult,
            totalSizeResult,
            appSizesResult
        );

        if (mergedResults.IsFailed) return mergedResults;
        return Result.Ok(
            new LibraryFolder
            {
                Path = pathResult.Value,
                Label = labelResult.Value,
                TotalDiskSize = totalSizeResult.Value,
                AppSizes = appSizesResult.Value,
            }
        );
    }
}
