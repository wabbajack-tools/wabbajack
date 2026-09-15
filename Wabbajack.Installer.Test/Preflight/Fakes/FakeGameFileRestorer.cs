#nullable enable
using System;
using System.Collections.Generic;
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

    public GameFileRestorerStatus Status()
    {
        return new GameFileRestorerStatus(Ready, Ready ? "Logged in as tester" : NotReadyReason);
    }

    public async Task<GameFileRestoreResult> Restore(Game game, string? version, RelativePath gameFile,
        AbsolutePath output, CancellationToken token)
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
