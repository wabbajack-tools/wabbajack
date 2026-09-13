#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Installer.Test.Preflight.Fakes;

/// <summary>
///     What the fake downloaders serve. One instance is shared by every test class in the process, so tests
///     key their archives by URLs nobody else uses (a fresh GUID in the path does it). A state nobody has
///     registered here is passed through to the real downloader.
/// </summary>
public sealed class FakeDownloadServer
{
    private sealed class Entry
    {
        public byte[]? Bytes { get; set; }
        public ConcurrentQueue<Func<Exception>> Failures { get; } = new();
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _attempts = new();

    public static string Key(IDownloadState state)
    {
        return state switch
        {
            Http http => http.Url.ToString(),
            WabbajackCDN cdn => cdn.Url.ToString(),
            Nexus nexus => $"nexus:{nexus.Game}:{nexus.ModID}:{nexus.FileID}",
            _ => state.PrimaryKeyString
        };
    }

    /// <summary>Every download of this state writes these bytes, once any queued failures are spent.</summary>
    public void Serve(IDownloadState state, byte[] bytes)
    {
        Get(state).Bytes = bytes;
    }

    /// <summary>The next <paramref name="times" /> downloads of this state throw what the factory makes.</summary>
    public void FailNext(IDownloadState state, int times, Func<Exception> factory)
    {
        var entry = Get(state);
        for (var i = 0; i < times; i++)
            entry.Failures.Enqueue(factory);
    }

    public bool Knows(IDownloadState state)
    {
        return _entries.ContainsKey(Key(state));
    }

    public int Attempts(IDownloadState state)
    {
        var key = Key(state);
        return _attempts.Count(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> AllAttempts => _attempts.ToList();

    public async Task<Hash> Download(IDownloadState state, AbsolutePath destination, IJob job, CancellationToken token)
    {
        var key = Key(state);
        _attempts.Enqueue(key);
        var entry = _entries[key];
        if (entry.Failures.TryDequeue(out var factory))
            throw factory();

        var bytes = entry.Bytes ?? throw new InvalidOperationException($"{key} has failures queued but nothing to serve");
        job.Size = bytes.Length;
        destination.Parent.CreateDirectory();
        await destination.WriteAllBytesAsync(bytes, token);
        await job.Report(bytes.Length, token);
        return await bytes.Hash();
    }

    private Entry Get(IDownloadState state)
    {
        return _entries.GetOrAdd(Key(state), _ => new Entry());
    }
}
