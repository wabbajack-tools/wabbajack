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
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.VFS;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.CLI.Verbs;

public class HashGameFiles
{
    private readonly ILogger<HashGameFiles> _logger;
    private readonly GameLocator _gameLocator;
    private readonly FileHashCache _cache;
    private readonly DTOSerializer _dtos;

    public HashGameFiles(
        ILogger<HashGameFiles> logger,
        GameLocator gameLocator,
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

        var version = "Unknown";
        if (gameMeta.MainExecutable == null)
        {
            _logger.LogError("Could not find Main Executable for {Game}", gameEnum);
            return 1;
        }
        try
        {
            var mainExe = gameLocation.Combine(gameMeta.MainExecutable);
            var info = FileVersionInfo.GetVersionInfo(mainExe.ToString());
            version = info.ProductVersion ?? info.FileVersion ?? version;
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
        return 0;
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
