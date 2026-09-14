using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.VFS;

public class FileHashCache
{
    private readonly SQLiteConnection _conn;
    private readonly string _connectionString;
    private readonly IResource<FileHashCache> _limiter;
    private readonly AbsolutePath _location;

    /// <summary>
    ///     One connection, one statement at a time. Hashing runs on every thread at once and every lookup,
    ///     write and purge goes through this one connection. A VACUUM cannot start while another statement on
    ///     that connection is still open, and the VACUUM is the one that fails, with "SQL logic error". The
    ///     lock has to cover every statement rather than only the vacuum, because what a vacuum needs is for
    ///     nothing else to be in flight.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileHashCache(AbsolutePath location, IResource<FileHashCache> limiter)
    {
        _limiter = limiter;
        _location = location;

        if (!_location.Parent.DirectoryExists())
            _location.Parent.CreateDirectory();

        _connectionString =
            string.Intern($"URI=file:{_location};Pooling=True;Max Pool Size=100; Journal Mode=Memory;");
        _conn = new SQLiteConnection(_connectionString);
        _conn.Open();


        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS HashCache (
            Path TEXT PRIMARY KEY,
            LastModified BIGINT,
            Hash BIGINT)
            WITHOUT ROWID";
        cmd.ExecuteNonQuery();

        AddSizeColumnIfMissing();
    }

    /// <summary>
    ///     Size was added to the cache after the fact. It is added in place rather than by rebuilding the
    ///     table, so that entries written by an earlier version survive: a user with a large downloads folder
    ///     on a slow disk should not have to rehash all of it to pick up this change. Rows from before the
    ///     column existed hold NULL, which <see cref="TryGetHashCache" /> treats as "size unknown".
    /// </summary>
    private void AddSizeColumnIfMissing()
    {
        using var check = new SQLiteCommand(_conn);
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('HashCache') WHERE name = 'Size'";
        if (System.Convert.ToInt64(check.ExecuteScalar()) > 0) return;

        using var alter = new SQLiteCommand(_conn);
        alter.CommandText = "ALTER TABLE HashCache ADD COLUMN Size BIGINT";
        alter.ExecuteNonQuery();
    }

    private async Task<(AbsolutePath Path, long LastModified, Hash Hash, long? Size)> Get(AbsolutePath path)
    {
        await _lock.WaitAsync();
        try
        {
            using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = "SELECT LastModified, Hash, Size FROM HashCache WHERE Path = @path";
            cmd.Parameters.AddWithValue("@path", path.ToString().ToLowerInvariant());
            await cmd.PrepareAsync();

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                return (path, reader.GetInt64(0), Hash.FromLong(reader.GetInt64(1)),
                    reader.IsDBNull(2) ? null : reader.GetInt64(2));

            return default;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Blocks until the connection is free. Callers that may be on the UI thread want
    ///     <see cref="PurgeAsync" /> instead: blocking there while a lock holder is waiting to resume on that
    ///     same thread would hang the application.
    /// </summary>
    public void Purge(AbsolutePath path)
    {
        _lock.Wait();
        try
        {
            PurgeLocked(path);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task PurgeAsync(AbsolutePath path)
    {
        await _lock.WaitAsync();
        try
        {
            PurgeLocked(path);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void PurgeLocked(AbsolutePath path)
    {
        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = "DELETE FROM HashCache WHERE Path = @path";
        cmd.Parameters.AddWithValue("@path", path.ToString().ToLowerInvariant());
        cmd.Prepare();

        cmd.ExecuteNonQuery();
    }

    private async Task Upsert(AbsolutePath path, long lastModified, Hash hash, long size)
    {
        await _lock.WaitAsync();
        try
        {
            await using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText =
                @"INSERT INTO HashCache (Path, LastModified, Hash, Size) VALUES (@path, @lastModified, @hash, @size)
            ON CONFLICT(Path) DO UPDATE SET LastModified = @lastModified, Hash = @hash, Size = @size";
            cmd.Parameters.AddWithValue("@path", path.ToString().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@lastModified", lastModified);
            cmd.Parameters.AddWithValue("@hash", (long) hash);
            cmd.Parameters.AddWithValue("@size", size);
            await cmd.PrepareAsync();

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Fills in the size of a row written before the column existed, so it gains the same protection as a
    ///     freshly written one without the file being read again.
    /// </summary>
    private async Task BackfillSize(AbsolutePath path, long size)
    {
        await _lock.WaitAsync();
        try
        {
            await using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = "UPDATE HashCache SET Size = @size WHERE Path = @path AND Size IS NULL";
            cmd.Parameters.AddWithValue("@size", size);
            cmd.Parameters.AddWithValue("@path", path.ToString().ToLowerInvariant());
            await cmd.PrepareAsync();

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public void VacuumDatabase()
    {
        _lock.Wait();
        try
        {
            using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"VACUUM";
            cmd.Prepare();

            cmd.ExecuteNonQuery();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Hash> TryGetHashCache(AbsolutePath file)
    {
        // new FileInfo("") throws where File.Exists("") answered false, and callers used to be able to ask
        // about a path they had not filled in.
        if (file == default) return default;

        // Existence, both timestamps and the size all come from one FileInfo. Each of FileExists(),
        // LastModifiedUtc(), CreatedUtc() and Size() is its own call into the file system, and a hit on
        // this cache made six of them; hashing a downloads folder does this once per file.
        var info = file.Info();
        if (!info.Exists) return default;

        var result = await Get(file);
        if (result == default || result.Hash == default)
            return default;

        // Taken again once the row is in hand, because that is when it used to be read. A file rewritten
        // while the row was being fetched - new bytes, a new timestamp, the same length - would otherwise be
        // answered from a snapshot that predates it, and the size column exists precisely to catch damage a
        // timestamp does not. Both halves of the check now come from one snapshot, so they describe the same
        // moment, which reading them one at a time never guaranteed.
        info.Refresh();

        // Fix for strange issue where dates are messed up on some systems
        if (info.LastWriteTimeUtc < info.CreationTimeUtc)
        {
            file.Touch();
            info.Refresh();
        }

        if (result.LastModified != info.LastWriteTimeUtc.ToFileTimeUtc())
        {
            await PurgeAsync(file);
            return default;
        }

        // A file damaged in place often keeps its timestamp, so the timestamp alone does not establish that
        // the cached hash still describes the file. Size is cheap to check and catches truncation.
        var size = info.Length;

        if (result.Size == null)
        {
            // Written before the column existed. Trusted as it always has been, and given a size now so that
            // later damage is caught.
            await BackfillSize(file, size);
            return result.Hash;
        }

        if (result.Size != size)
        {
            await PurgeAsync(file);
            return default;
        }

        return result.Hash;
    }

    private async Task WriteHashCache(AbsolutePath file, Hash hash)
    {
        if (file == default) return;

        var info = file.Info();
        if (!info.Exists) return;
        await Upsert(file, info.LastWriteTimeUtc.ToFileTimeUtc(), hash, info.Length);
    }

    public async Task FileHashWriteCache(AbsolutePath file, Hash hash)
    {
        await WriteHashCache(file, hash);
    }

    public async Task<Hash> FileHashCachedAsync(AbsolutePath file, CancellationToken token)
    {
        var hash = await TryGetHashCache(file);
        if (hash != default) return hash;

        using var job = await _limiter.Begin($"Hashing {file.FileName}", file.Info().Length, token);
        await using var fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

        hash = await fs.HashingCopy(Stream.Null, token, job);
        if (hash != default)
            await WriteHashCache(file, hash);
        return hash;
    }
}