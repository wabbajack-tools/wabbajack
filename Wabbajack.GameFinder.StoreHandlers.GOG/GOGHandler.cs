using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wabbajack.GameFinder.Common;
using Wabbajack.GameFinder.RegistryUtils;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;
using OneOf;

namespace Wabbajack.GameFinder.StoreHandlers.GOG;

/// <summary>
/// Handler for finding games installed with GOG Galaxy.
/// </summary>
[PublicAPI]
public class GOGHandler : AHandler<GOGGame, GOGGameId>
{
    internal const string GOGRegKey = @"Software\GOG.com\Games";

    private readonly IRegistry _registry;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="registry">
    /// The implementation of <see cref="IRegistry"/> to use. For a shared instance
    /// use <see cref="WindowsRegistry.Shared"/> on Windows. For tests either use
    /// <see cref="InMemoryRegistry"/>, a custom implementation or just a mock
    /// of the interface. See the README for more information if you want to use
    /// Wine.
    /// </param>
    /// <param name="fileSystem">
    /// The implementation of <see cref="IFileSystem"/> to use. For a shared instance use
    /// <see cref="FileSystem.Shared"/>. For tests either use <see cref="InMemoryFileSystem"/>,
    /// a custom implementation or just a mock of the interface. See the README for more information
    /// if you want to use Wine.
    /// </param>
    public GOGHandler(IRegistry registry, IFileSystem fileSystem)
    {
        _registry = registry;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public override Func<GOGGame, GOGGameId> IdSelector => game => game.Id;

    /// <inheritdoc/>
    public override IEqualityComparer<GOGGameId>? IdEqualityComparer => null;

    /// <inheritdoc/>
    public override IEnumerable<OneOf<GOGGame, ErrorMessage>> FindAllGames()
    {
        try
        {
            var localMachine = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            using var gogKey = localMachine.OpenSubKey(GOGRegKey);
            if (gogKey is null)
            {
                return new OneOf<GOGGame, ErrorMessage>[]
                {
                    new ErrorMessage($"Unable to open HKEY_LOCAL_MACHINE\\{GOGRegKey}"),
                };
            }

            var subKeyNames = gogKey.GetSubKeyNames().ToArray();
            if (subKeyNames.Length == 0)
            {
                return new OneOf<GOGGame, ErrorMessage>[]
                {
                    new ErrorMessage($"Registry key {gogKey.GetName()} has no sub-keys"),
                };
            }

            return subKeyNames
                .Select(subKeyName => ParseSubKey(gogKey, subKeyName))
                .ToArray();
        }
        catch (Exception e)
        {
            return new OneOf<GOGGame, ErrorMessage>[]
            {
                new ErrorMessage(e, "Exception looking for GOG games"),
            };
        }
    }

    private OneOf<GOGGame, ErrorMessage> ParseSubKey(IRegistryKey gogKey, string subKeyName)
    {
        try
        {
            using var subKey = gogKey.OpenSubKey(subKeyName);
            if (subKey is null)
            {
                return new ErrorMessage($"Unable to open {gogKey}\\{subKeyName}");
            }

            var idResult = GetId(subKey, subKeyName);
            if (idResult.IsT1) return idResult.AsT1;

            if (!subKey.TryGetString("gameName", out var name))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"gameName\"");
            }

            if (!subKey.TryGetString("path", out var path))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"path\"");
            }

            if (!subKey.TryGetString("buildId", out var sBuildId))
            {
                return new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"buildId\"");
            }

            if (!ulong.TryParse(sBuildId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var buildId))
            {
                return new ErrorMessage($"The value \"buildId\" of {subKey.GetName()} is not a number: \"{sBuildId}\"");
            }

            Nullable<GOGGameId> parentGameId = null;
            if (subKey.TryGetString("dependsOn", out var dependsOn))
            {
                if (long.TryParse(dependsOn, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawParentGameId))
                {
                    parentGameId = GOGGameId.From(rawParentGameId);
                }
            }

            var game = new GOGGame(
                GOGGameId.From(idResult.AsT0),
                name,
                _fileSystem.FromUnsanitizedFullPath(path),
                buildId,
                parentGameId
            );

            return game;
        }
        catch (Exception e)
        {
            return new ErrorMessage(e, $"Exception while parsing registry key {gogKey}\\{subKeyName}");
        }
    }

    private static OneOf<long, ErrorMessage> GetId(IRegistryKey subKey, string subKeyName)
    {
        ErrorMessage? subKeyError = null;
        ErrorMessage? idError = null;

        if (!long.TryParse(subKeyName, CultureInfo.InvariantCulture, out var subKeyId))
        {
            subKeyError = new ErrorMessage($"`{subKeyName}` of `{subKey.GetName()}` is not a number!");
        }

        long id = -1;
        if (subKey.TryGetString("gameID", out var sID))
        {
            if (!long.TryParse(sID, CultureInfo.InvariantCulture, out id))
            {
                idError = new ErrorMessage($"The value \"gameID\" of {subKey.GetName()} is not a number: \"{sID}\"");
            }
        }
        else
        {
            idError = new ErrorMessage($"{subKey.GetName()} doesn't have a string value \"gameID\"");
        }

        if (subKeyError.HasValue && idError.HasValue)
        {
            return new ErrorMessage($"{subKeyError.Value} | {idError.Value}");
        }

        if (subKeyError.HasValue && !idError.HasValue) return id;
        if (!subKeyError.HasValue && idError.HasValue) return subKeyId;
        return id;
    }
}
