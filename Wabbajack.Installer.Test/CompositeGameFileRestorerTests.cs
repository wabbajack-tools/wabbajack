#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test;

/// <summary>
///     The rules that let two game file sources sit behind the one <see cref="IGameFileRestorer" /> the
///     runner resolves, and the one rule that matters most: a source that is not set up must not speak for
///     the ones that are.
/// </summary>
public class CompositeGameFileRestorerTests
{
    private static readonly RelativePath Wanted = "Data\\ccBGSSSE002-ExoticArrows.esl".ToRelativePath();

    private static CompositeGameFileRestorer Composite(params IGameFileRestorer[] restorers)
    {
        return new CompositeGameFileRestorer(NullLogger.Instance, restorers);
    }

    private static Task<GameFileRestoreResult> Restore(CompositeGameFileRestorer composite,
        AbsolutePath output = default)
    {
        return composite.Restore(Game.SkyrimSpecialEdition, null, Wanted, output, CancellationToken.None);
    }

    [Fact]
    public void ACompositeNeedsAtLeastOneSource()
    {
        Assert.Throws<ArgumentException>(() => Composite());
    }

    /// <summary>
    ///     Whether a game that is not installed could be fetched instead, across sources. One source that
    ///     can is all preflight needs, so a source that carries add-ons rather than the game - which is what
    ///     Bethesda's Creations are - must not be able to answer for the one that carries the game.
    /// </summary>
    [Fact]
    public async Task OneSourceThatCanSupplyTheGameIsEnough()
    {
        var addOns = Sourcing("Bethesda", GameSourceOutcome.NoSource);
        var depots = Sourcing("Steam", GameSourceOutcome.Available);

        var result = await Composite(addOns, depots).CanSourceGame(Game.SkyrimSpecialEdition,
            CancellationToken.None);

        Assert.True(result.Available);
        Assert.Equal("Steam", result.Reason);
    }

    /// <summary>
    ///     When none can, what the user is told comes from a source that actually asked. "We do not carry
    ///     that game" is not an answer about the account, and it must not shadow one that is.
    /// </summary>
    [Fact]
    public async Task AnAnswerAboutTheAccountBeatsASourceThatDidNotLook()
    {
        var result = await Composite(Sourcing("Bethesda", GameSourceOutcome.NoSource),
                Sourcing("Steam", GameSourceOutcome.NotOwned))
            .CanSourceGame(Game.SkyrimSpecialEdition, CancellationToken.None);

        Assert.Equal(GameSourceOutcome.NotOwned, result.Outcome);
    }

    /// <summary>
    ///     A store that would not say has not contradicted one that said no, and an install stopped over
    ///     "you do not own this" should only ever be stopped by an account somebody actually read.
    /// </summary>
    [Fact]
    public async Task ASourceThatCouldNotFindOutOutranksOneThatSaysNo()
    {
        var result = await Composite(Sourcing("Steam", GameSourceOutcome.NotOwned),
                Sourcing("Other", GameSourceOutcome.Unconfirmed))
            .CanSourceGame(Game.SkyrimSpecialEdition, CancellationToken.None);

        Assert.Equal(GameSourceOutcome.Unconfirmed, result.Outcome);
    }

    /// <summary>
    ///     With nobody logged in anywhere, the reported answer is the one the user can act on: logging in is
    ///     something they can go and do, and "this game is not ours" leaves them nowhere.
    /// </summary>
    [Fact]
    public async Task ALoginTheUserCouldMakeOutranksASourceThatCarriesNothing()
    {
        var result = await Composite(Sourcing("Bethesda", GameSourceOutcome.NoSource),
                Sourcing("Steam", GameSourceOutcome.NotReady))
            .CanSourceGame(Game.SkyrimSpecialEdition, CancellationToken.None);

        Assert.Equal(GameSourceOutcome.NotReady, result.Outcome);
        Assert.Equal("Steam", result.Reason);
    }

    private static ScriptedRestorer Sourcing(string name, GameSourceOutcome outcome)
    {
        return new ScriptedRestorer(name, GameFileRestoreOutcome.FileNotFound) {Source = outcome};
    }

    [Fact]
    public async Task SourcesAreAskedInTheOrderTheyWereGiven()
    {
        var first = new ScriptedRestorer("Steam", GameFileRestoreOutcome.FileNotFound);
        var second = new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.FileNotFound);

        await Restore(Composite(first, second));

        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
        Assert.True(first.AskedAt < second.AskedAt);
    }

    [Fact]
    public async Task TheFirstSourceToProduceTheFileWins()
    {
        var first = new ScriptedRestorer("Steam", GameFileRestoreOutcome.Fetched);
        var second = new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.Fetched);

        var result = await Restore(Composite(first, second));

        Assert.True(result.Fetched);
        Assert.Equal("Steam", result.Detail);
        Assert.Equal(0, second.Calls);
    }

    /// <summary>
    ///     The reason this class exists. <c>GameFileRepair.One</c> stops dead on <c>NotReady</c>, and
    ///     <c>SteamGameFileRestorer</c> answers <c>NotReady</c> to every request when nobody has logged into
    ///     Steam through Wabbajack. Passing that up would leave a user with Steam running and no Wabbajack
    ///     login unable to fetch a single Creation - the exact user the Bethesda source was built for.
    /// </summary>
    [Fact]
    public async Task ASourceThatIsNotSetUpDoesNotStopTheOnesThatAre()
    {
        var steam = new ScriptedRestorer("Steam", GameFileRestoreOutcome.NotReady);
        var bethesda = new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.Fetched);

        var result = await Restore(Composite(steam, bethesda));

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);
        Assert.Equal("Bethesda", result.Detail);
    }

    [Theory]
    [InlineData(GameFileRestoreOutcome.NotReady)]
    [InlineData(GameFileRestoreOutcome.NoSource)]
    [InlineData(GameFileRestoreOutcome.FileNotFound)]
    [InlineData(GameFileRestoreOutcome.VersionUnknown)]
    [InlineData(GameFileRestoreOutcome.Failed)]
    public async Task EveryOutcomeShortOfFetchedFallsThrough(GameFileRestoreOutcome outcome)
    {
        var first = new ScriptedRestorer("Steam", outcome);
        var second = new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.Fetched);

        var result = await Restore(Composite(first, second));

        Assert.True(result.Fetched);
        Assert.Equal(1, second.Calls);
    }

    /// <summary>
    ///     A source that breaks has not established that the file is unobtainable, only that it could not
    ///     hand it over, so the next source is still asked. A depot bug is the ordinary way this happens:
    ///     one shipped here where a decompression fault failed every chunk of twenty-odd files that were in
    ///     the depot the whole time. Ending the search there would keep a second source in the tree and
    ///     refuse to use it exactly when it is needed.
    /// </summary>
    [Fact]
    public async Task AFailureDoesNotStopTheChain()
    {
        var first = new ScriptedRestorer("Steam", GameFileRestoreOutcome.Failed);
        var second = new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.Fetched);

        var result = await Restore(Composite(first, second));

        Assert.True(result.Fetched);
        Assert.Equal("Bethesda", result.Detail);
        Assert.Equal(1, second.Calls);
    }

    /// <summary>
    ///     Falling through must not cost the user the real error. A source that broke still outranks one
    ///     that simply had nothing, so "Steam could not decompress it" is what gets reported rather than
    ///     "Bethesda does not publish it".
    /// </summary>
    [Fact]
    public async Task ABreakageOutranksASourceThatSimplyDidNotHaveTheFile()
    {
        var result = await Restore(Composite(
            new ScriptedRestorer("Steam", GameFileRestoreOutcome.Failed),
            new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.FileNotFound)));

        Assert.Equal(GameFileRestoreOutcome.Failed, result.Outcome);
        Assert.Equal("Steam", result.Detail);
    }

    [Fact]
    public async Task NotReadyIsReportedOnlyWhenEverySourceIsUnready()
    {
        var result = await Restore(Composite(
            new ScriptedRestorer("Steam", GameFileRestoreOutcome.NotReady),
            new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.NotReady)));

        Assert.Equal(GameFileRestoreOutcome.NotReady, result.Outcome);
    }

    /// <summary>
    ///     A source that went and looked has more to say than one that never tried, so its answer is the one
    ///     reported - which is also what keeps <c>GameFileRepair</c> from reading the whole repair as "not
    ///     attempted" and skipping its second version attempt.
    /// </summary>
    [Fact]
    public async Task AnAnswerFromASourceThatTriedBeatsOneThatDidNot()
    {
        var result = await Restore(Composite(
            new ScriptedRestorer("Steam", GameFileRestoreOutcome.NotReady),
            new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.FileNotFound)));

        Assert.Equal(GameFileRestoreOutcome.FileNotFound, result.Outcome);
        Assert.Equal("Bethesda", result.Detail);
    }

    [Fact]
    public async Task NotReadyOutranksNoSourceWhenNobodyTried()
    {
        var result = await Restore(Composite(
            new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.NoSource),
            new ScriptedRestorer("Steam", GameFileRestoreOutcome.NotReady)));

        Assert.Equal(GameFileRestoreOutcome.NotReady, result.Outcome);
    }

    [Fact]
    public async Task NoSourceEverywhereStaysNoSource()
    {
        var result = await Restore(Composite(
            new ScriptedRestorer("Steam", GameFileRestoreOutcome.NoSource),
            new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.NoSource)));

        Assert.Equal(GameFileRestoreOutcome.NoSource, result.Outcome);
    }

    /// <summary>
    ///     The ranking <c>GameFileRepair.Informativeness</c> uses, applied across sources: a version nobody
    ///     indexed is something the user can act on, and "no manifest lists that file" is what both sources
    ///     say when neither has it.
    /// </summary>
    [Fact]
    public async Task TheMostInformativeAnswerIsTheOneReported()
    {
        var result = await Restore(Composite(
            new ScriptedRestorer("Steam", GameFileRestoreOutcome.FileNotFound),
            new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.VersionUnknown)));

        Assert.Equal(GameFileRestoreOutcome.VersionUnknown, result.Outcome);
    }

    [Fact]
    public async Task TiesKeepTheEarlierSource()
    {
        var result = await Restore(Composite(
            new ScriptedRestorer("Steam", GameFileRestoreOutcome.FileNotFound),
            new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.FileNotFound)));

        Assert.Equal("Steam", result.Detail);
    }

    [Fact]
    public void StatusIsReadyWhenAnySourceIs()
    {
        var composite = Composite(
            new ScriptedRestorer("Steam", GameFileRestoreOutcome.NotReady) {Ready = false, Reason = "Log into Steam."},
            new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.Fetched) {Ready = true, Reason = "Steam is running."});

        var status = composite.Status();

        Assert.True(status.Ready);
        Assert.Equal("Steam is running.", status.Reason);
    }

    [Fact]
    public void StatusGivesEveryReasonWhenNothingIsReady()
    {
        var composite = Composite(
            new ScriptedRestorer("Steam", GameFileRestoreOutcome.NotReady) {Ready = false, Reason = "Log into Steam."},
            new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.NotReady) {Ready = false, Reason = "Start Steam."});

        var status = composite.Status();

        Assert.False(status.Ready);
        Assert.Contains("Log into Steam.", status.Reason);
        Assert.Contains("Start Steam.", status.Reason);
    }

    [Fact]
    public void ConsequencesAreTheUnionOfEverySource()
    {
        var steam = new ScriptedRestorer("Steam", GameFileRestoreOutcome.Fetched)
            {Consequence = "This adds the Creation Kit to your Steam library."};
        var bethesda = new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.Fetched)
            {Consequence = "Steam has to be running and signed in."};

        var said = Composite(steam, bethesda).Consequences(new[] {Game.SkyrimSpecialEdition});

        Assert.Equal(2, said.Count);
        Assert.Contains("This adds the Creation Kit to your Steam library.", said);
        Assert.Contains("Steam has to be running and signed in.", said);
    }

    [Fact]
    public void TheSameConsequenceFromTwoSourcesIsSaidOnce()
    {
        var said = Composite(
                new ScriptedRestorer("Steam", GameFileRestoreOutcome.Fetched) {Consequence = "Steam must be running."},
                new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.Fetched) {Consequence = "Steam must be running."})
            .Consequences(new[] {Game.SkyrimSpecialEdition});

        Assert.Single(said);
    }

    /// <summary>
    ///     The argument is an <c>IEnumerable</c> and callers pass a LINQ projection over the repair list, so
    ///     it has to survive being read once per source.
    /// </summary>
    [Fact]
    public void AGamesSequenceThatCanOnlyBeReadOnceIsStillReadByEverySource()
    {
        var reads = 0;

        IEnumerable<Game> Games()
        {
            reads++;
            yield return Game.SkyrimSpecialEdition;
        }

        var said = Composite(
                new ScriptedRestorer("Steam", GameFileRestoreOutcome.Fetched) {Consequence = "One."},
                new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.Fetched) {Consequence = "Two."})
            .Consequences(Games());

        Assert.Equal(2, said.Count);
        Assert.Equal(1, reads);
    }

    [Fact]
    public void SourceNameNamesEverySource()
    {
        Assert.Equal("Steam and Bethesda", Composite(
            new ScriptedRestorer("Steam", GameFileRestoreOutcome.Fetched),
            new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.Fetched)).SourceName);

        Assert.Equal("Steam, Bethesda and GOG", Composite(
            new ScriptedRestorer("Steam", GameFileRestoreOutcome.Fetched),
            new ScriptedRestorer("Bethesda", GameFileRestoreOutcome.Fetched),
            new ScriptedRestorer("GOG", GameFileRestoreOutcome.Fetched)).SourceName);
    }

    /// <summary>
    ///     A host that registers one source has to behave exactly as it did before there was anything to
    ///     compose, which includes the name every message about a fetch carries.
    /// </summary>
    [Fact]
    public void OneRegisteredSourceIsUsedAsItself()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddGameFileRestorer<FirstRestorer>(GameFileRestorerOrder.Steam);

        var restorer = services.BuildServiceProvider().GetRequiredService<IGameFileRestorer>();

        Assert.IsType<FirstRestorer>(restorer);
        Assert.Equal("First", restorer.SourceName);
    }

    [Fact]
    public void TwoRegisteredSourcesAreComposedInOrderOfTheirOrder()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        // Registered the wrong way round on purpose: the order is the number, not the call.
        services.AddGameFileRestorer<SecondRestorer>(GameFileRestorerOrder.Bethesda);
        services.AddGameFileRestorer<FirstRestorer>(GameFileRestorerOrder.Steam);

        var composite = Assert.IsType<CompositeGameFileRestorer>(
            services.BuildServiceProvider().GetRequiredService<IGameFileRestorer>());

        Assert.Equal(new[] {"First", "Second"}, composite.Restorers.Select(r => r.SourceName).ToArray());
    }

    [Fact]
    public void RegisteringTheSameSourceTwiceAddsItOnce()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddGameFileRestorer<FirstRestorer>(GameFileRestorerOrder.Steam);
        services.AddGameFileRestorer<FirstRestorer>(GameFileRestorerOrder.Steam);
        services.AddGameFileRestorer<SecondRestorer>(GameFileRestorerOrder.Bethesda);

        var composite = Assert.IsType<CompositeGameFileRestorer>(
            services.BuildServiceProvider().GetRequiredService<IGameFileRestorer>());

        Assert.Equal(2, composite.Restorers.Count);
    }

    [Fact]
    public void AHostThatSuppliesItsOwnRestorerKeepsIt()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<IGameFileRestorer, SecondRestorer>();
        services.AddGameFileRestorer<FirstRestorer>(GameFileRestorerOrder.Steam);

        Assert.IsType<SecondRestorer>(services.BuildServiceProvider().GetRequiredService<IGameFileRestorer>());
    }

    /// <summary>Two collections in one process must not share a chain.</summary>
    [Fact]
    public void TwoServiceCollectionsKeepSeparateChains()
    {
        var first = new ServiceCollection();
        first.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        first.AddGameFileRestorer<FirstRestorer>(GameFileRestorerOrder.Steam);
        first.AddGameFileRestorer<SecondRestorer>(GameFileRestorerOrder.Bethesda);

        var second = new ServiceCollection();
        second.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        second.AddGameFileRestorer<FirstRestorer>(GameFileRestorerOrder.Steam);

        Assert.IsType<CompositeGameFileRestorer>(first.BuildServiceProvider().GetRequiredService<IGameFileRestorer>());
        Assert.IsType<FirstRestorer>(second.BuildServiceProvider().GetRequiredService<IGameFileRestorer>());
    }

    /// <summary>A source that answers whatever it was told to, and records that it was asked.</summary>
    private sealed class ScriptedRestorer : IGameFileRestorer
    {
        private static int _clock;
        private readonly GameFileRestoreOutcome _outcome;

        public ScriptedRestorer(string name, GameFileRestoreOutcome outcome)
        {
            SourceName = name;
            _outcome = outcome;
        }

        public bool Ready { get; init; } = true;
        public string Reason { get; init; } = "ready";
        public string? Consequence { get; init; }

        /// <summary>What this source says when asked whether it could stand in for a missing install.</summary>
        public GameSourceOutcome Source { get; init; } = GameSourceOutcome.NoSource;

        public int Calls { get; private set; }
        public int AskedAt { get; private set; }

        public string SourceName { get; }

        public GameFileRestorerStatus Status()
        {
            return new GameFileRestorerStatus(Ready, Reason);
        }

        public IReadOnlyList<string> Consequences(IEnumerable<Game> games)
        {
            return Consequence == null || !games.Any() ? Array.Empty<string>() : new[] {Consequence};
        }

        public Task<GameSourceResult> CanSourceGame(Game game, CancellationToken token)
        {
            Calls++;
            AskedAt = Interlocked.Increment(ref _clock);
            return Task.FromResult(new GameSourceResult(Source, SourceName));
        }

        public async Task<GameFileRestoreResult> Restore(Game game, string? version, RelativePath gameFile,
            AbsolutePath output, CancellationToken token, long? expectedSize = null)
        {
            Calls++;
            AskedAt = Interlocked.Increment(ref _clock);

            if (_outcome == GameFileRestoreOutcome.Fetched && output != default)
            {
                output.Parent.CreateDirectory();
                await output.WriteAllBytesAsync(Encoding.UTF8.GetBytes(SourceName), token);
            }

            // The detail is the source's name so a test can say which one answered.
            return new GameFileRestoreResult(_outcome, null, SourceName);
        }
    }

    /// <summary>Two named types, because the registry keys on the type and DI resolves by it.</summary>
    private sealed class FirstRestorer : NamedRestorer
    {
        public override string SourceName => "First";
    }

    private sealed class SecondRestorer : NamedRestorer
    {
        public override string SourceName => "Second";
    }

    private abstract class NamedRestorer : IGameFileRestorer
    {
        public abstract string SourceName { get; }

        public GameFileRestorerStatus Status()
        {
            return new GameFileRestorerStatus(true, SourceName);
        }

        public IReadOnlyList<string> Consequences(IEnumerable<Game> games)
        {
            return Array.Empty<string>();
        }

        public Task<GameSourceResult> CanSourceGame(Game game, CancellationToken token)
        {
            return Task.FromResult(new GameSourceResult(GameSourceOutcome.NoSource, SourceName));
        }

        public Task<GameFileRestoreResult> Restore(Game game, string? version, RelativePath gameFile,
            AbsolutePath output, CancellationToken token, long? expectedSize = null)
        {
            return Task.FromResult(new GameFileRestoreResult(GameFileRestoreOutcome.FileNotFound, null, SourceName));
        }
    }
}
