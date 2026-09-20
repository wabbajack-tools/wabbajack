#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Test.Preflight.Fakes;

/// <summary>
///     A game file source that answers from a table instead of a depot.
///     <see cref="Files" /> is keyed by the version asked for and the file wanted, which is exactly the
///     question <see cref="IGameFileRestorer" /> exists to ask: a key with a null version is what the game
///     publishes now, and a key with a version is what the index would have resolved. A version with no
///     entries at all reads as one the index does not carry.
/// </summary>
public sealed class FakeGameFileRestorer : IGameFileRestorer
{
    /// <summary>Bytes to write, by (version, depot-relative path). Null version means the current build.</summary>
    public Dictionary<(string? Version, string File), string> Files { get; } = new();

    /// <summary>Versions the index knows about. A version outside this reads as unindexed.</summary>
    public HashSet<string> KnownVersions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every (game, version, file) this was asked for, in order.</summary>
    public List<(Game Game, string? Version, string File)> Asked { get; } = new();

    public bool Ready { get; set; } = true;

    public string NotReadyReason { get; set; } = "Log into Steam and Wabbajack can fetch these for you.";

    /// <summary>Set to make every attempt throw, standing in for a depot that falls over mid-fetch.</summary>
    public Func<Exception>? Throws { get; set; }

    public string SourceName => "Steam";

    /// <summary>What <see cref="Consequences" /> says, for the games in <see cref="ConsequentialGames" />.</summary>
    public string Consequence { get; set; } = "This adds the Creation Kit to your Steam library.";

    /// <summary>Games whose repair carries <see cref="Consequence" />. Empty, which is the ordinary case.</summary>
    public HashSet<Game> ConsequentialGames { get; } = new();

    /// <summary>
    ///     What this answers when asked whether it could stand in for a game that is not installed. NoSource
    ///     by default, so a test has to opt in to the install-without-the-game path rather than fall into it.
    /// </summary>
    public GameSourceResult GameSource { get; set; } =
        new(GameSourceOutcome.NoSource, "This source does not carry that game.");

    /// <summary>Every game <see cref="CanSourceGame" /> was asked about, in order.</summary>
    public List<Game> AskedToSource { get; } = new();

    /// <summary>Set to make <see cref="CanSourceGame" /> throw, standing in for a store nobody can reach.</summary>
    public Func<Exception>? SourceThrows { get; set; }

    public GameFileRestorerStatus Status()
    {
        return new GameFileRestorerStatus(Ready, Ready ? "Logged in as tester" : NotReadyReason);
    }

    public Task<GameSourceResult> CanSourceGame(Game game, CancellationToken token)
    {
        AskedToSource.Add(game);
        if (SourceThrows != null) throw SourceThrows();
        return Task.FromResult(GameSource);
    }

    public IReadOnlyList<string> Consequences(IEnumerable<Game> games)
    {
        return games.Any(ConsequentialGames.Contains)
            ? new[] {Consequence}
            : Array.Empty<string>();
    }

    public async Task<GameFileRestoreResult> Restore(Game game, string? version, RelativePath gameFile,
        AbsolutePath output, CancellationToken token, long? expectedSize = null)
    {
        var wanted = gameFile.ToString();
        Asked.Add((game, version, wanted));

        if (!Ready) return new GameFileRestoreResult(GameFileRestoreOutcome.NotReady, version, NotReadyReason);
        if (Throws != null) throw Throws();

        if (version != null && !KnownVersions.Contains(version))
            return new GameFileRestoreResult(GameFileRestoreOutcome.VersionUnknown, version,
                $"The index has no record of {game} {version}.");

        if (!Files.TryGetValue((version, wanted), out var content))
            return new GameFileRestoreResult(GameFileRestoreOutcome.FileNotFound, version,
                $"Nothing published at {version ?? "the current build"} contains \"{wanted}\".");

        output.Parent.CreateDirectory();
        await output.WriteAllBytesAsync(Encoding.UTF8.GetBytes(content), token);
        return new GameFileRestoreResult(GameFileRestoreOutcome.Fetched, version, "depot 1, manifest 2");
    }
}
