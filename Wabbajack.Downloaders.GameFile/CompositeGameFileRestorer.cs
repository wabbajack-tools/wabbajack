using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Downloaders.GameFile;

/// <summary>
///     Several game file sources behind one <see cref="IGameFileRestorer" />, asked in order.
///     <para>
///         A game's files do not all come from one place. Skyrim Special Edition's depots carry the game,
///         the Creation Kit and four of the Anniversary Edition Creations; the other seventy Creations are
///         published by Bethesda as a runtime download and are in no depot at all. Neither source can
///         answer for the other, and the caller - a preflight check, a repair - has no business knowing
///         that. So it asks one restorer, and this is the one it asks.
///     </para>
///     <para>
///         Everything here is about not letting one source's "no" stand for all of them. That is the whole
///         reason the class exists, and the case that matters most is <see cref="GameFileRestoreOutcome.NotReady" />:
///         <c>GameFileRepair</c> stops the moment a restorer reports it, so a Steam restorer with no stored
///         Wabbajack login returning <c>NotReady</c> would, if passed straight up, leave a user with Steam
///         running and no Wabbajack login unable to fetch a single Creation - and that is precisely the
///         user the Bethesda source exists for.
///     </para>
/// </summary>
public sealed class CompositeGameFileRestorer : IGameFileRestorer
{
    private readonly ILogger _logger;
    private readonly IReadOnlyList<IGameFileRestorer> _restorers;

    /// <param name="restorers">The sources, in the order they are worth asking.</param>
    public CompositeGameFileRestorer(ILogger logger, IReadOnlyList<IGameFileRestorer> restorers)
    {
        if (restorers.Count == 0)
            throw new ArgumentException("A composite restorer needs at least one source.", nameof(restorers));

        _logger = logger;
        _restorers = restorers;
    }

    /// <summary>The sources, in the order they are asked. Exposed so a host can report what it has.</summary>
    public IReadOnlyList<IGameFileRestorer> Restorers => _restorers;

    /// <summary>All of them, because a file could have come from any one of them.</summary>
    public string SourceName => Join(_restorers.Select(r => r.SourceName).Distinct().ToArray());

    /// <summary>
    ///     Ready when any source is, because one working source is all a fetch needs.
    ///     <para>
    ///         The reason follows from that. When something is ready the user is told what is ready and
    ///         nothing else: an unready source's reason is an instruction ("log into Steam and...") and
    ///         reading one under a row that says the fetch can go ahead is confusing rather than helpful.
    ///         Its reason is not lost - it comes back attached to the file that actually needed that
    ///         source. When nothing is ready every reason is given, because each one is a different thing
    ///         the user could go and do, and they should pick.
    ///     </para>
    /// </summary>
    public GameFileRestorerStatus Status()
    {
        var statuses = _restorers.Select(r => r.Status()).ToArray();
        var ready = statuses.Where(s => s.Ready).ToArray();

        return ready.Length > 0
            ? new GameFileRestorerStatus(true, Sentences(ready.Select(s => s.Reason)))
            : new GameFileRestorerStatus(false, Sentences(statuses.Select(s => s.Reason)));
    }

    /// <summary>
    ///     Available as soon as one source says so: one source that can hand the game's files over is all
    ///     an install without the game needs.
    ///     <para>
    ///         What is reported when none can is the same rule <see cref="Restore" /> follows. A source that
    ///         actually asked - it knows the account does not own the game, or could not find out - has said
    ///         something about this game, and that beats a source that declined to look at all. Among
    ///         declines, <see cref="GameSourceOutcome.NotReady" /> beats
    ///         <see cref="GameSourceOutcome.NoSource" />, because one is something the user can go and fix
    ///         and the other is "this game is not ours".
    ///     </para>
    /// </summary>
    public async Task<GameSourceResult> CanSourceGame(Game game, CancellationToken token)
    {
        var declined = new List<GameSourceResult>();
        var answered = new List<GameSourceResult>();

        foreach (var restorer in _restorers)
        {
            token.ThrowIfCancellationRequested();

            var result = await restorer.CanSourceGame(game, token);
            if (result.Available) return result;

            _logger.LogDebug("{Source} cannot stand in for {Game}: {Outcome} ({Reason})", restorer.SourceName,
                game, result.Outcome, result.Reason);

            if (result.Outcome is GameSourceOutcome.NotReady or GameSourceOutcome.NoSource)
                declined.Add(result);
            else
                answered.Add(result);
        }

        // Unconfirmed over NotOwned: a source that could not find out has not contradicted one that says no,
        // and an install stopped over "you do not own this" should only ever be stopped by an account that
        // was actually read.
        if (answered.Count > 0)
            return answered.FirstOrDefault(a => a.Outcome == GameSourceOutcome.Unconfirmed) ?? answered[0];

        return declined.FirstOrDefault(d => d.Outcome == GameSourceOutcome.NotReady) ?? declined[0];
    }

    /// <summary>
    ///     The union. Each source speaks about a different thing it would do to the user's account - a
    ///     Steam licence taken for a free companion app, a Bethesda fetch that wants the Steam client
    ///     signed in - and a repair may reach either, so both have to be in front of the user while they
    ///     are deciding.
    /// </summary>
    public IReadOnlyList<string> Consequences(IEnumerable<Game> games)
    {
        // Materialised once: the argument may be a lazy sequence and every source is about to read it.
        var asked = games.ToArray();
        return _restorers.SelectMany(r => r.Consequences(asked)).Distinct().ToArray();
    }

    /// <summary>
    ///     Asks each source in turn and returns the first that produced the file.
    ///     <para>
    ///         A source that did not try - it is not set up (<see cref="GameFileRestoreOutcome.NotReady" />)
    ///         or does not serve this game at all (<see cref="GameFileRestoreOutcome.NoSource" />) - and a
    ///         source that tried and did not have the file
    ///         (<see cref="GameFileRestoreOutcome.FileNotFound" />,
    ///         <see cref="GameFileRestoreOutcome.VersionUnknown" />) both fall through to the next one, and
    ///         so does <see cref="GameFileRestoreOutcome.Failed" />. A source that broke has said something
    ///         specific and keeps its place in the ranking below, but it has not said that the file is
    ///         unobtainable - only that this source could not hand it over. A depot bug is the ordinary way
    ///         that happens: one shipped here where a decompression fault failed every chunk of twenty-odd
    ///         files that were sitting in the depot the whole time. Letting that end the search would put a
    ///         second source in the tree and then decline to use it exactly when it is needed.
    ///     </para>
    ///     <para>
    ///         What is reported when nothing worked is the important part. An answer from a source that
    ///         actually tried always beats a "did not try", so <c>NotReady</c> is reported only when
    ///         <em>every</em> source declined - which is what keeps <c>GameFileRepair</c> from treating one
    ///         unconfigured source as the end of the whole repair. Among sources that did try, the ranking
    ///         is the one <c>GameFileRepair.Informativeness</c> uses: a named version nobody indexed tells
    ///         the user more than "no manifest lists that file", which is what every source says when
    ///         another one has the real answer.
    ///     </para>
    /// </summary>
    public async Task<GameFileRestoreResult> Restore(Game game, string? version, RelativePath gameFile,
        AbsolutePath output, CancellationToken token)
    {
        var declined = new List<GameFileRestoreResult>();
        var answered = new List<GameFileRestoreResult>();

        foreach (var restorer in _restorers)
        {
            token.ThrowIfCancellationRequested();

            var result = await restorer.Restore(game, version, gameFile, output, token);
            if (result.Fetched) return result;

            _logger.LogDebug("{Source} did not supply {File}: {Outcome} ({Detail})", restorer.SourceName,
                gameFile, result.Outcome, result.Detail);

            if (result.Outcome is GameFileRestoreOutcome.NotReady or GameFileRestoreOutcome.NoSource)
                declined.Add(result);
            else
                answered.Add(result);
        }

        // Something looked, so what it found - or did not - is the answer. A source that never tried has
        // nothing to say about a file another source went and searched for.
        if (answered.Count > 0) return answered.MaxBy(Informativeness)!;

        // Nobody tried. NotReady over NoSource, because one is something the user can fix and the other is
        // "this game is not ours"; a user told the second when the first is true would have no way back in.
        return declined.FirstOrDefault(d => d.Outcome == GameFileRestoreOutcome.NotReady)
               ?? declined[0];
    }

    /// <summary>
    ///     How much a failed attempt tells the user, highest first. The same ranking
    ///     <c>GameFileRepair.Informativeness</c> applies across the two version attempts it makes, applied
    ///     here across sources, and for the same reason: "no manifest lists that file" is what every source
    ///     says when one of the others is holding the real answer.
    /// </summary>
    private static int Informativeness(GameFileRestoreResult result)
    {
        return result.Outcome switch
        {
            GameFileRestoreOutcome.VersionUnknown => 2,
            GameFileRestoreOutcome.FileNotFound => 0,
            _ => 1
        };
    }

    private static string Sentences(IEnumerable<string> reasons)
    {
        return string.Join(" ", reasons.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct());
    }

    /// <summary>"Steam", "Steam and Bethesda", "Steam, Bethesda and GOG".</summary>
    private static string Join(IReadOnlyList<string> names)
    {
        return names.Count switch
        {
            0 => "nowhere",
            1 => names[0],
            _ => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}"
        };
    }
}
