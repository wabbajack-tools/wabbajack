using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Networking.Bethesda.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     What a helper run meant, decided without a helper. Everything here is the parent half: which exit code
///     is which failure, what a missing ticket line implies, and what happens when the child does not come
///     back.
/// </summary>
public class ChildProcessSteamAppTicketSourceTests : IDisposable
{
    private const uint SkyrimSpecialEdition = 489830;

    private static readonly string TheGameName =
        Game.SkyrimSpecialEdition.MetaData().HumanFriendlyGameName;

    private readonly AbsolutePath _gameFolder;
    private readonly DirectoryInfo _root;

    public ChildProcessSteamAppTicketSourceTests()
    {
        _root = Directory.CreateTempSubdirectory("wj-ticket-");
        _gameFolder = _root.FullName.ToAbsolutePath();
        File.WriteAllBytes(_gameFolder.Combine(SteamAppTicketMinter.LibraryName).ToString(), Array.Empty<byte>());
    }

    public void Dispose()
    {
        try
        {
            _root.Delete(true);
        }
        catch (IOException)
        {
            // A temp folder that will not go is the test host's problem, not the test's.
        }
    }

    private ChildProcessSteamAppTicketSource Source(ISteamAppTicketHelper helper, AbsolutePath? folder = null,
        SteamAppTicketOptions? options = null)
    {
        return new ChildProcessSteamAppTicketSource(NullLogger<ChildProcessSteamAppTicketSource>.Instance,
            new FakeGameLocator(folder ?? _gameFolder), helper, options ?? new SteamAppTicketOptions());
    }

    [Fact]
    public async Task AMintedTicketComesBackAsBytes()
    {
        var helper = FakeHelper.Succeeding(SteamAppTicketOutput.Format(new byte[] {1, 2, 3, 4}));

        var ticket = await Source(helper).GetEncryptedAppTicket(SkyrimSpecialEdition, CancellationToken.None);

        Assert.Equal(new byte[] {1, 2, 3, 4}, ticket);
    }

    [Fact]
    public async Task TheHelperIsToldWhichAppAndWhereTheLibraryIs()
    {
        var helper = FakeHelper.Succeeding(SteamAppTicketOutput.Format(new byte[] {1}));

        await Source(helper).GetEncryptedAppTicket(SkyrimSpecialEdition, CancellationToken.None);

        AssertContainsInOrder(helper.Arguments, ChildProcessSteamAppTicketSource.Verb, "--app", "489830");
        AssertContainsInOrder(helper.Arguments, "--folder", _gameFolder.ToString());
    }

    [Theory]
    [InlineData(SteamAppTicketError.SteamNotRunning)]
    [InlineData(SteamAppTicketError.InitFailed)]
    [InlineData(SteamAppTicketError.InterfaceUnavailable)]
    [InlineData(SteamAppTicketError.NotEntitled)]
    [InlineData(SteamAppTicketError.TicketRefused)]
    [InlineData(SteamAppTicketError.TicketTimedOut)]
    [InlineData(SteamAppTicketError.SteamApiMissing)]
    [InlineData(SteamAppTicketError.GameNotFound)]
    public async Task TheHelpersExitCodeIsTheFailureItReports(SteamAppTicketError error)
    {
        var helper = new FakeHelper(new SteamAppTicketHelperResult(error.ToExitCode(), string.Empty, string.Empty));

        var thrown = await Assert.ThrowsAsync<SteamAppTicketException>(async () =>
            await Source(helper).GetEncryptedAppTicket(SkyrimSpecialEdition, CancellationToken.None));

        Assert.Equal(error, thrown.Error);
        Assert.Equal(error.DefaultMessage(TheGameName), thrown.Message);
    }

    /// <summary>What steam_api64.dll writes to standard error before the helper says anything, as captured live.</summary>
    private const string SteamApiChatter =
        "Setting breakpad minidump AppID = 489830\n" +
        "SteamInternal_SetMinidumpSteamID:  Caching Steam ID:  76561198000000000 [API loaded no]\n";

    [Fact]
    public async Task WhatTheHelperSaidBeatsTheGenericSentence()
    {
        var helper = new FakeHelper(new SteamAppTicketHelperResult(
            SteamAppTicketError.TicketRefused.ToExitCode(), string.Empty,
            SteamApiChatter + ChildProcessSteamAppTicketSource.MessagePrefix +
            "Steam refused to issue an app ticket for Skyrim Special Edition: " +
            "Steam has no connection to its servers (offline mode, or the network is down).\n"));

        var thrown = await Assert.ThrowsAsync<SteamAppTicketException>(async () =>
            await Source(helper).GetEncryptedAppTicket(SkyrimSpecialEdition, CancellationToken.None));

        Assert.Equal(SteamAppTicketError.TicketRefused, thrown.Error);
        Assert.StartsWith("Steam refused to issue an app ticket", thrown.Message);
        Assert.Contains("no connection to its servers", thrown.Message);
    }

    /// <summary>
    ///     steam_api64.dll prints the account's SteamID64 on the helper's standard error. The error message ends
    ///     up in logs users post, so only the helper's own marked lines make it into one.
    /// </summary>
    [Fact]
    public async Task TheSteamIdSteamPrintsNeverReachesTheMessage()
    {
        var helper = new FakeHelper(new SteamAppTicketHelperResult(
            SteamAppTicketError.TicketRefused.ToExitCode(), string.Empty, SteamApiChatter));

        var thrown = await Assert.ThrowsAsync<SteamAppTicketException>(async () =>
            await Source(helper).GetEncryptedAppTicket(SkyrimSpecialEdition, CancellationToken.None));

        Assert.DoesNotContain("76561198000000000", thrown.Message);
        Assert.DoesNotContain("breakpad", thrown.Message);
        Assert.Equal(SteamAppTicketError.TicketRefused.DefaultMessage(TheGameName), thrown.Message);
    }

    [Fact]
    public async Task AnExitCodeNobodyChoseIsTheHelperFailing()
    {
        var helper = new FakeHelper(new SteamAppTicketHelperResult(1, string.Empty, string.Empty));

        var thrown = await Assert.ThrowsAsync<SteamAppTicketException>(async () =>
            await Source(helper).GetEncryptedAppTicket(SkyrimSpecialEdition, CancellationToken.None));

        Assert.Equal(SteamAppTicketError.HelperFailed, thrown.Error);
    }

    [Fact]
    public async Task SucceedingWithNoTicketIsAFailure()
    {
        var helper = FakeHelper.Succeeding("0.1 [INFO] all done, honest");

        var thrown = await Assert.ThrowsAsync<SteamAppTicketException>(async () =>
            await Source(helper).GetEncryptedAppTicket(SkyrimSpecialEdition, CancellationToken.None));

        Assert.Equal(SteamAppTicketError.HelperFailed, thrown.Error);
    }

    [Fact]
    public async Task AnAppIdNobodyKnowsIsAnsweredBeforeAnythingIsRun()
    {
        var helper = FakeHelper.Succeeding(SteamAppTicketOutput.Format(new byte[] {1}));

        var thrown = await Assert.ThrowsAsync<SteamAppTicketException>(async () =>
            await Source(helper).GetEncryptedAppTicket(1, CancellationToken.None));

        Assert.Equal(SteamAppTicketError.GameNotFound, thrown.Error);
        Assert.Contains("1", thrown.Message);
        Assert.Equal(0, helper.Runs);
    }

    [Fact]
    public async Task AGameThatIsNotInstalledIsAnsweredBeforeAnythingIsRun()
    {
        var helper = FakeHelper.Succeeding(SteamAppTicketOutput.Format(new byte[] {1}));

        var thrown = await Assert.ThrowsAsync<SteamAppTicketException>(async () =>
            await Source(helper, default(AbsolutePath))
                .GetEncryptedAppTicket(SkyrimSpecialEdition, CancellationToken.None));

        Assert.Equal(SteamAppTicketError.GameNotFound, thrown.Error);
        Assert.Equal(0, helper.Runs);
    }

    [Fact]
    public async Task AGameFolderWithoutTheLibraryIsAnsweredBeforeAnythingIsRun()
    {
        var empty = Directory.CreateTempSubdirectory("wj-ticket-empty-");
        try
        {
            var helper = FakeHelper.Succeeding(SteamAppTicketOutput.Format(new byte[] {1}));

            var thrown = await Assert.ThrowsAsync<SteamAppTicketException>(async () =>
                await Source(helper, empty.FullName.ToAbsolutePath())
                    .GetEncryptedAppTicket(SkyrimSpecialEdition, CancellationToken.None));

            Assert.Equal(SteamAppTicketError.SteamApiMissing, thrown.Error);
            Assert.Contains(SteamAppTicketMinter.LibraryName, thrown.Message);
            Assert.Equal(0, helper.Runs);
        }
        finally
        {
            empty.Delete(true);
        }
    }

    [Fact]
    public async Task AHelperThatNeverComesBackIsEndedAndReported()
    {
        var options = new SteamAppTicketOptions
        {
            TicketTimeout = TimeSpan.FromMilliseconds(50), HelperGrace = TimeSpan.FromMilliseconds(50)
        };

        var thrown = await Assert.ThrowsAsync<SteamAppTicketException>(async () =>
            await Source(new HangingHelper(), options: options)
                .GetEncryptedAppTicket(SkyrimSpecialEdition, CancellationToken.None));

        Assert.Equal(SteamAppTicketError.HelperFailed, thrown.Error);
        Assert.Contains("did not finish", thrown.Message);
    }

    [Fact]
    public async Task TheCallersOwnCancellationIsNotDressedUpAsAFailure()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Source(new HangingHelper()).GetEncryptedAppTicket(SkyrimSpecialEdition, cancelled.Token));
    }

    /// <summary>
    ///     The items appear in <paramref name="actual" /> in the given order, not necessarily contiguously
    ///     or exclusively.
    /// </summary>
    private static void AssertContainsInOrder(IReadOnlyList<string> actual, params string[] expected)
    {
        var searchFrom = 0;
        foreach (var item in expected)
        {
            var index = -1;
            for (var i = searchFrom; i < actual.Count; i++)
            {
                if (actual[i] == item)
                {
                    index = i;
                    break;
                }
            }

            Assert.True(index >= 0,
                $"Expected to find \"{item}\" at or after index {searchFrom} in [{string.Join(", ", actual)}]");
            searchFrom = index + 1;
        }
    }

    private sealed class FakeGameLocator : IGameLocator
    {
        private readonly AbsolutePath _location;

        public FakeGameLocator(AbsolutePath location)
        {
            _location = location;
        }

        public AbsolutePath GameLocation(Game game)
        {
            return _location;
        }

        public bool IsInstalled(Game game)
        {
            return _location != default;
        }

        public bool TryFindLocation(Game game, out AbsolutePath path)
        {
            path = _location;
            return _location != default;
        }

        public bool TryGetSteamBuildId(Game game, out string buildId)
        {
            buildId = string.Empty;
            return false;
        }

        public bool TryGetSteamManifests(Game game, out SteamManifest[] manifests)
        {
            manifests = Array.Empty<SteamManifest>();
            return false;
        }
    }

    private sealed class FakeHelper : ISteamAppTicketHelper
    {
        private readonly SteamAppTicketHelperResult _result;

        public FakeHelper(SteamAppTicketHelperResult result)
        {
            _result = result;
        }

        public IReadOnlyList<string> Arguments { get; private set; } = Array.Empty<string>();
        public int Runs { get; private set; }

        public Task<SteamAppTicketHelperResult> Run(IReadOnlyList<string> arguments, CancellationToken token)
        {
            Arguments = arguments;
            Runs++;
            return Task.FromResult(_result);
        }

        public static FakeHelper Succeeding(string standardOutput)
        {
            return new FakeHelper(new SteamAppTicketHelperResult(0, standardOutput, string.Empty));
        }
    }

    private sealed class HangingHelper : ISteamAppTicketHelper
    {
        public async Task<SteamAppTicketHelperResult> Run(IReadOnlyList<string> arguments, CancellationToken token)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("unreachable");
        }
    }
}
