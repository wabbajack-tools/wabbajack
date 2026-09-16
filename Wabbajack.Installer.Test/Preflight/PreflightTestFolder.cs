#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>
///     A throwaway folder tree for preflight tests, with helpers for building archives and the files that
///     satisfy them. Each test class owns one; the <see cref="TemporaryFileManager" /> registered in DI is a
///     singleton that another test class disposes, so it is not used here.
/// </summary>
public sealed class PreflightTestFolder : IDisposable
{
    private readonly TemporaryFileManager _manager;

    public PreflightTestFolder()
    {
        _manager = new TemporaryFileManager(KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()));
        Root = _manager.CreateFolder().Path;
        Watch = Root.Combine("watch");
        Watch.CreateDirectory();
        Destination = Root.Combine("downloads");
        Destination.CreateDirectory();
    }

    public AbsolutePath Root { get; }

    /// <summary>Stands in for the user's Downloads folder.</summary>
    public AbsolutePath Watch { get; }

    /// <summary>Stands in for the install's downloads folder.</summary>
    public AbsolutePath Destination { get; }

    public AbsolutePath NewFolder(string name)
    {
        var folder = Root.Combine(name);
        folder.CreateDirectory();
        return folder;
    }

    /// <summary>Deterministic content; the same seed and length always give the same bytes.</summary>
    public static byte[] Bytes(int seed, int length)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    public static async Task<Archive> ArchiveFor(string name, byte[] bytes)
    {
        return new Archive
        {
            Name = name,
            Size = bytes.Length,
            Hash = await bytes.Hash(),
            State = new DTOs.DownloadStates.Manual {Url = new Uri($"https://example.invalid/{name}"), Prompt = ""}
        };
    }

    public static async Task<AbsolutePath> WriteFile(AbsolutePath folder, string name, byte[] bytes)
    {
        var path = folder.Combine(name);
        await path.WriteAllBytesAsync(bytes);
        return path;
    }

    /// <summary>
    ///     Polls until the condition holds; fails the test with <paramref name="what" /> if it never does. The
    ///     default is far longer than any of this work takes, deliberately: a timeout that is only a few times
    ///     the expected duration turns a busy CI runner into a failing build, and a generous one costs wall
    ///     clock only when the test was going to fail anyway.
    /// </summary>
    public static async Task WaitUntil(Func<bool> condition, string what, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(30);
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > limit)
                throw new TimeoutException($"Timed out after {limit.TotalSeconds:0}s waiting for: {what}");
            await Task.Delay(20);
        }
    }

    /// <summary>Waits long enough for the acquirer to have acted if it were going to; then the caller asserts it did not.</summary>
    public static Task SettleTime()
    {
        return Task.Delay(600);
    }

    public void Dispose()
    {
        _manager.Dispose();
    }
}
