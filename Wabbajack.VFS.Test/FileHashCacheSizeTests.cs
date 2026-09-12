using System;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.VFS.Test;

/// <summary>
///     The cache decides whether a file still matches its stored hash without reading the file, so what it
///     validates on decides which damaged archives get through. These cover the size check and, in
///     particular, the handling of rows written before the Size column existed: those must keep working
///     untouched, because invalidating them would make every existing user rehash a downloads folder that is
///     often hundreds of gigabytes on a slow disk.
/// </summary>
public class FileHashCacheSizeTests
{
    private readonly TemporaryFileManager _manager;

    public FileHashCacheSizeTests(TemporaryFileManager manager)
    {
        _manager = manager;
    }

    private FileHashCache NewCache(AbsolutePath location)
    {
        return new FileHashCache(location, new Resource<FileHashCache>("Test hashing", 2));
    }

    private AbsolutePath NewCacheLocation()
    {
        return _manager.CreateFolder().Path.Combine("hashcache.sqlite");
    }

    /// <summary>
    ///     Writes a row in the shape used before Size existed: the column is present but NULL.
    /// </summary>
    private static void SetSizeToNull(AbsolutePath cacheLocation, AbsolutePath file)
    {
        using var conn = new SQLiteConnection($"URI=file:{cacheLocation};Pooling=False;");
        conn.Open();
        using var cmd = new SQLiteCommand(conn);
        cmd.CommandText = "UPDATE HashCache SET Size = NULL WHERE Path = @path";
        cmd.Parameters.AddWithValue("@path", file.ToString().ToLowerInvariant());
        cmd.ExecuteNonQuery();
    }

    private static long? ReadStoredSize(AbsolutePath cacheLocation, AbsolutePath file)
    {
        using var conn = new SQLiteConnection($"URI=file:{cacheLocation};Pooling=False;");
        conn.Open();
        using var cmd = new SQLiteCommand(conn);
        cmd.CommandText = "SELECT Size FROM HashCache WHERE Path = @path";
        cmd.Parameters.AddWithValue("@path", file.ToString().ToLowerInvariant());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return reader.IsDBNull(0) ? null : reader.GetInt64(0);
    }

    /// <summary>
    ///     Rewrites a file in place and restores its timestamp, which is what a crash or failing disk tends to
    ///     leave behind.
    /// </summary>
    private static async Task OverwriteKeepingTimestamp(AbsolutePath file, string content)
    {
        var original = file.LastModifiedUtc();
        await file.WriteAllTextAsync(content);
        File.SetLastWriteTimeUtc(file.ToString(), original);
    }

    [Fact]
    public async Task ARowWrittenBeforeTheSizeColumnIsStillTrusted()
    {
        var location = NewCacheLocation();
        var cache = NewCache(location);

        var file = _manager.CreateFile();
        await file.Path.WriteAllTextAsync("Cheese for Everyone!");

        var hash = await cache.FileHashCachedAsync(file.Path, CancellationToken.None);
        Assert.NotEqual(default, hash);

        SetSizeToNull(location, file.Path);

        // No rehash, no purge: the row answers exactly as it did before the column was added.
        Assert.Equal(hash, await cache.TryGetHashCache(file.Path));
    }

    [Fact]
    public async Task ARowWrittenBeforeTheSizeColumnGetsItsSizeFilledIn()
    {
        var location = NewCacheLocation();
        var cache = NewCache(location);

        var file = _manager.CreateFile();
        await file.Path.WriteAllTextAsync("Cheese for Everyone!");
        await cache.FileHashCachedAsync(file.Path, CancellationToken.None);

        SetSizeToNull(location, file.Path);
        Assert.Null(ReadStoredSize(location, file.Path));

        await cache.TryGetHashCache(file.Path);

        Assert.Equal(file.Path.Size(), ReadStoredSize(location, file.Path));
    }

    /// <summary>
    ///     Having been filled in, the row protects the file the same way a freshly written one does.
    /// </summary>
    [Fact]
    public async Task AfterBackfillTruncationIsCaught()
    {
        var location = NewCacheLocation();
        var cache = NewCache(location);

        var file = _manager.CreateFile();
        await file.Path.WriteAllTextAsync("Cheese for Everyone!");
        await cache.FileHashCachedAsync(file.Path, CancellationToken.None);

        SetSizeToNull(location, file.Path);
        await cache.TryGetHashCache(file.Path);

        await OverwriteKeepingTimestamp(file.Path, "Cheese");

        Assert.Equal(default, await cache.TryGetHashCache(file.Path));
    }

    [Fact]
    public async Task TruncationWithAPreservedTimestampIsCaught()
    {
        var location = NewCacheLocation();
        var cache = NewCache(location);

        var file = _manager.CreateFile();
        await file.Path.WriteAllTextAsync("Cheese for Everyone!");
        await cache.FileHashCachedAsync(file.Path, CancellationToken.None);

        await OverwriteKeepingTimestamp(file.Path, "Cheese");

        Assert.Equal(default, await cache.TryGetHashCache(file.Path));
    }

    /// <summary>
    ///     Documents what the size check does not cover. Damage that keeps both the length and the timestamp
    ///     still reads as valid, because catching it would mean rereading every file on every run.
    /// </summary>
    [Fact]
    public async Task SameLengthDamageWithAPreservedTimestampIsStillMissed()
    {
        var location = NewCacheLocation();
        var cache = NewCache(location);

        var file = _manager.CreateFile();
        await file.Path.WriteAllTextAsync("Cheese for Everyone!");
        var hash = await cache.FileHashCachedAsync(file.Path, CancellationToken.None);

        await OverwriteKeepingTimestamp(file.Path, "Cheese for everyone?");

        Assert.Equal(hash, await cache.TryGetHashCache(file.Path));
    }

    [Fact]
    public async Task AnUnchangedFileStillHitsTheCache()
    {
        var location = NewCacheLocation();
        var cache = NewCache(location);

        var file = _manager.CreateFile();
        await file.Path.WriteAllTextAsync("Cheese for Everyone!");

        var hash = await cache.FileHashCachedAsync(file.Path, CancellationToken.None);
        Assert.Equal(hash, await cache.TryGetHashCache(file.Path));
        Assert.Equal(hash, await cache.TryGetHashCache(file.Path));
    }

    [Fact]
    public async Task AChangedTimestampStillInvalidates()
    {
        var location = NewCacheLocation();
        var cache = NewCache(location);

        var file = _manager.CreateFile();
        await file.Path.WriteAllTextAsync("Cheese for Everyone!");
        await cache.FileHashCachedAsync(file.Path, CancellationToken.None);

        await file.Path.WriteAllTextAsync("Cheese for Everyone!");
        File.SetLastWriteTimeUtc(file.Path.ToString(), DateTime.UtcNow.AddMinutes(5));

        Assert.Equal(default, await cache.TryGetHashCache(file.Path));
    }

    /// <summary>
    ///     Opening a database created by the previous schema must add the column and leave the rows alone.
    /// </summary>
    [Fact]
    public async Task OpeningAPreviousSchemaDatabaseKeepsItsRows()
    {
        var location = NewCacheLocation();

        var file = _manager.CreateFile();
        await file.Path.WriteAllTextAsync("Cheese for Everyone!");
        var expected = await file.Path.Hash(CancellationToken.None);

        // Build the database exactly as the previous version did, with no Size column at all.
        await using (var conn = new SQLiteConnection($"URI=file:{location};Pooling=False;"))
        {
            await conn.OpenAsync();

            await using (var create = new SQLiteCommand(conn))
            {
                create.CommandText = @"CREATE TABLE IF NOT EXISTS HashCache (
                    Path TEXT PRIMARY KEY,
                    LastModified BIGINT,
                    Hash BIGINT)
                    WITHOUT ROWID";
                await create.ExecuteNonQueryAsync();
            }

            await using var insert = new SQLiteCommand(conn);
            insert.CommandText =
                "INSERT INTO HashCache (Path, LastModified, Hash) VALUES (@path, @lastModified, @hash)";
            insert.Parameters.AddWithValue("@path", file.Path.ToString().ToLowerInvariant());
            insert.Parameters.AddWithValue("@lastModified", file.Path.LastModifiedUtc().ToFileTimeUtc());
            insert.Parameters.AddWithValue("@hash", (long) expected);
            await insert.ExecuteNonQueryAsync();
        }

        SQLiteConnection.ClearAllPools();

        var cache = NewCache(location);

        Assert.Equal(expected, await cache.TryGetHashCache(file.Path));
    }
}
