using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Networking.Bethesda;
using Wabbajack.Networking.Bethesda.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.CLI.Test;

/// <summary>
///     The Bethesda chain end to end: a ticket minted by the Steam client on this machine, a session, a
///     resolve, a real <c>.ckm</c> fetched and unpacked, and one of its two files written out.
///     <para>
///         Needs Steam running, signed in to an account that owns Skyrim Special Edition and the Anniversary
///         Upgrade, and the game installed. It deliberately does <em>not</em> need a stored Wabbajack Steam
///         login - that is the whole point of this path, so the test builds its own container with no Steam
///         session in it and proves the fetch works anyway.
///     </para>
///     <para>
///         The Creation fetched is whichever of the seventy-four is smallest, worked out from the sizes in
///         the resolve response rather than hardcoded, so the proof costs the least traffic available on the
///         day.
///     </para>
/// </summary>
[Trait("Category", "RequiresNetwork")]
public class CreationRestorerLiveTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _provider;
    private readonly AbsolutePath _root;

    public CreationRestorerLiveTests(ITestOutputHelper output)
    {
        _output = output;
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "wj-creation-live-" + Guid.NewGuid().ToString("N")[..8]).ToAbsolutePath();
        _root.CreateDirectory();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<HttpClient>();
        services.AddSingleton(new TemporaryFileManager(_root.Combine("temp")));

        // The real locator, not the stubbed folders the CLI fixture uses: the ticket is minted by the
        // library that ships in the game's own folder, so there has to be a real one.
        services.AddStandardGameLocator();

        services.AddBethesdaCreations();
        services.AddSteamAppTicket();

        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        if (!_root.DirectoryExists()) return;
        try
        {
            _root.DeleteDirectory();
        }
        catch
        {
            // best effort
        }
    }

    /// <summary>
    ///     With no Steam source registered at all, the one restorer in the chain is this one - so a fetch
    ///     here is a fetch with no Wabbajack Steam login anywhere in reach.
    /// </summary>
    [Fact]
    public void TheOnlySourceHereIsBethesda()
    {
        var restorer = _provider.GetRequiredService<IGameFileRestorer>();

        Assert.Equal("Bethesda", restorer.SourceName);
        Assert.True(restorer.Status().Ready,
            "This test needs Steam running and Skyrim Special Edition installed: " + restorer.Status().Reason);
    }

    [Fact]
    public async Task ATicketFromTheRunningSteamClientBuysASession()
    {
        var api = _provider.GetRequiredService<BethesdaApiClient>();

        await api.SignIn(CancellationToken.None);

        Assert.True(api.HasSession);
    }

    [Fact]
    public async Task TheSmallestCreationIsFetchedUnpackedAndWritten()
    {
        var index = _provider.GetRequiredService<CreationIndex>();
        var api = _provider.GetRequiredService<BethesdaApiClient>();
        var restorer = _provider.GetRequiredService<IGameFileRestorer>();

        await api.SignIn(CancellationToken.None);
        var slots = await api.Resolve(index.ContentIds, CancellationToken.None);

        Assert.NotEmpty(slots);

        var smallest = slots.Where(s => s.Size is > 0).MinBy(s => s.Size!.Value);
        Assert.NotNull(smallest);

        var creation = index.Creations.First(c => c.ContentId == smallest!.ContentId);
        var plugin = $"Data\\{creation.Plugin}".ToRelativePath();
        var output = _root.Combine(creation.Plugin);

        var result = await restorer.Restore(Game.SkyrimSpecialEdition, null, plugin, output,
            CancellationToken.None);

        _output.WriteLine(
            $"{slots.Count} of {index.ContentIds.Count} Creations resolved; fetched {creation.DisplayName} " +
            $"({creation.Plugin}, content id {creation.ContentId}, {smallest!.Size} bytes) -> " +
            $"{(output.FileExists() ? output.Size() : 0)} bytes");

        Assert.Equal(GameFileRestoreOutcome.Fetched, result.Outcome);

        // Creations are not published per game build, so the result reports no version rather than
        // pretending the one it was asked for meant something.
        Assert.Null(result.Version);
        Assert.True(output.FileExists());
        Assert.True(output.Size() > 0);
    }

    /// <summary>
    ///     The reason the cache exists: the archive beside the plugin comes out of the same container, so
    ///     asking for it second costs no traffic at all.
    /// </summary>
    [Fact]
    public async Task TheSecondFileOfACreationCostsNoSecondDownload()
    {
        var index = _provider.GetRequiredService<CreationIndex>();
        var api = _provider.GetRequiredService<BethesdaApiClient>();
        var cache = _provider.GetRequiredService<CreationCache>();
        var restorer = _provider.GetRequiredService<IGameFileRestorer>();

        await api.SignIn(CancellationToken.None);
        var slots = await api.Resolve(index.ContentIds, CancellationToken.None);
        var smallest = slots.Where(s => s.Size is > 0).MinBy(s => s.Size!.Value);
        Assert.NotNull(smallest);

        var creation = index.Creations.First(c => c.ContentId == smallest!.ContentId);

        var first = await restorer.Restore(Game.SkyrimSpecialEdition, null,
            $"Data\\{creation.Plugin}".ToRelativePath(), _root.Combine(creation.Plugin), CancellationToken.None);
        Assert.Equal(GameFileRestoreOutcome.Fetched, first.Outcome);

        // Whatever else was in the container, asked for by the name it actually came out under rather than
        // by a guessed extension.
        var unpacked = await cache.Get(creation.ContentId, CancellationToken.None);
        Assert.NotNull(unpacked);
        var other = unpacked!.Files
            .First(f => !string.Equals(f.ToString(), creation.Plugin, StringComparison.OrdinalIgnoreCase));

        var second = await restorer.Restore(Game.SkyrimSpecialEdition, null, $"Data\\{other}".ToRelativePath(),
            _root.Combine(other), CancellationToken.None);

        Assert.Equal(GameFileRestoreOutcome.Fetched, second.Outcome);
        Assert.Equal(1, cache.Downloads);
    }
}
