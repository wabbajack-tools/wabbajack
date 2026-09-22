using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.VFS;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.CLI.Verbs;

public class HashGameFiles
{
    private readonly ILogger<HashGameFiles> _logger;
    private readonly IGameLocator _gameLocator;
    private readonly FileHashCache _cache;
    private readonly DTOSerializer _dtos;

    public HashGameFiles(
        ILogger<HashGameFiles> logger,
        IGameLocator gameLocator,
        FileHashCache cache,
        DTOSerializer dtos)
    {
        _logger = logger;
        _gameLocator = gameLocator;
        _cache = cache;
        _dtos = dtos;
    }

    public static VerbDefinition Definition = new VerbDefinition(
        "hash-game-files",
        "Hashes a game's files for inclusion in the public GitHub repo",
        new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "o", "output", "Output folder in which the file will be placed"),
            new OptionDefinition(typeof(string),       "g", "game",   "WJ Game to index")
        });

    internal async Task<int> Run(AbsolutePath output, string game, CancellationToken token)
    {
        var gameMeta = GameRegistry.GetByFuzzyName(game);
        var gameEnum = gameMeta.Game;

        AbsolutePath gameLocation;
        try
        {
            gameLocation = _gameLocator.GameLocation(gameEnum);
        }
        catch
        {
            _logger.LogError("Could not find installation for {Game}", gameEnum);
            return 1;
        }

        var version = "0.0.0.0";

        if (gameMeta.MainExecutable == null)
        {
            _logger.LogError("Could not find Main Executable for {Game}", gameEnum);
            return 1;
        }

        try
        {
            var mainExe = gameLocation.Combine(gameMeta.MainExecutable);
            var info = FileVersionInfo.GetVersionInfo(mainExe.ToString());

            var productVersion = info.ProductVersion?.Trim();
            var fileVersion = info.FileVersion?.Trim();

            // Some Unreal Engine games expose the engine changelist as ProductVersion
            // instead of an actual game version (for example "UE5-CL-0").
            var isUnrealEngineVersion =
                !string.IsNullOrWhiteSpace(productVersion) &&
                productVersion.StartsWith("UE", StringComparison.OrdinalIgnoreCase) &&
                productVersion.Contains("-CL-", StringComparison.OrdinalIgnoreCase);

            if (isUnrealEngineVersion &&
                _gameLocator.TryGetSteamBuildId(gameEnum, out var steamBuildId))
            {
                version = steamBuildId;
            }
            else
            {
                version = productVersion ?? fileVersion ?? version;
            }
        }
        catch
        {
            _logger.LogWarning("Could not determine version for {Game}", gameEnum);
        }

        var outFile = output
            .Combine(gameEnum.ToString(), version)
            .WithExtension(new Extension(".json"));
        outFile.Parent.CreateDirectory();

        _logger.LogInformation("Hashing files for {Game} {Version}", gameEnum, version);

        var results = new ConcurrentBag<Archive>();
        IEnumerable<AbsolutePath> files = gameLocation.EnumerateFiles();

        await Parallel.ForEachAsync(files, new ParallelOptions { CancellationToken = token }, async (f, ct) =>
        {
            try
            {
                var hash = await _cache.FileHashCachedAsync(f, ct);
                results.Add(new Archive
                {
                    State = new GameFileSource
                    {
                        Game = gameEnum,
                        GameFile = f.RelativeTo(gameLocation),
                        Hash = hash,
                        GameVersion = version
                    },
                    Name = f.FileName.ToString(),
                    Hash = hash,
                    Size = f.Size()
                });
            }
            catch { }
        });

        _logger.LogInformation("Found and hashed {Count} files", results.Count);

        var indexed = results.ToArray();
        await using var fs = File.Open(outFile.ToString(), FileMode.Create, FileAccess.Write, FileShare.None);

        var dtoArray = indexed.Select(a =>
        {
            var s = (GameFileSource)a.State;
            var pk = string.Join("|", s.PrimaryKey);
            return new ArchiveDto
            {
                Type = "Archive, Wabbajack.Lib",
                Hash = s.Hash,
                Meta = null!,
                Name = a.Name,
                Size = a.Size,
                State = new StateDto
                {
                    Type = "GameFileSourceDownloader, Wabbajack.Lib",
                    Game = s.Game,
                    GameFile = s.GameFile,
                    Hash = s.Hash,
                    GameVersion = s.GameVersion,
                    PrimaryKeyString = $"{s.TypeName}|{pk}"
                }
            };
        }).ToArray();

        await _dtos.Serialize(dtoArray, fs, writeIndented: true);

        _logger.LogInformation("Saved hash index to {Path}", outFile);

        await WriteSteamManifests(output, gameEnum, version, token);
        return 0;
    }

    /// <summary>
    ///     Writes the depot and manifest ids of the copy that was just hashed, beside the hashes.
    ///     <para>
    ///         This is the half of an indexed build that cannot be recovered later. The hashes can be
    ///         produced again by anyone who still has the files; the manifest ids exist only in the local
    ///         <c>appmanifest</c> and in Steam's memory of what it installed, and Steam will not say what a
    ///         depot published last year. So whoever indexes a build has to write them down while they have
    ///         it - after which <c>index-steam-depots</c> can read that build's depots from any machine, and
    ///         a repair can fetch its files without the user having the game at all.
    ///     </para>
    ///     <para>
    ///         Only for a game Steam installed. A GOG or retail copy has files to hash and no depots behind
    ///         them, and that is not a failure of anything.
    ///     </para>
    /// </summary>
    private async Task WriteSteamManifests(AbsolutePath output, Game game, string version,
        CancellationToken token)
    {
        if (!_gameLocator.TryGetSteamManifests(game, out var manifests))
        {
            _logger.LogInformation(
                "{Game} was not installed by Steam here, so there are no depot manifests to record", game);
            return;
        }

        var file = output.Combine(game.ToString(), $"{version}_steam_manifests")
            .WithExtension(Ext.Json);

        await using var fs = File.Open(file.ToString(), FileMode.Create, FileAccess.Write, FileShare.None);
        await _dtos.Serialize(manifests, fs, writeIndented: true);

        _logger.LogInformation("Recorded {Count} depot manifests for {Game} {Version} in {Path}",
            manifests.Length, game, version, file);
    }

    private class ArchiveDto
    {
        [JsonPropertyName("$type")] public string Type { get; set; }
        public Hash Hash { get; set; }
        public object Meta { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public StateDto State { get; set; }
    }

    private class StateDto
    {
        [JsonPropertyName("$type")] public string Type { get; set; }
        public Game Game { get; set; }
        public RelativePath GameFile { get; set; }
        public Hash Hash { get; set; }
        public string GameVersion { get; set; }
        public string PrimaryKeyString { get; set; }
    }
}
