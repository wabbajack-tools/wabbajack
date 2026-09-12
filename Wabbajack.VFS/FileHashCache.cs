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

    public void Purge(AbsolutePath path)
    {
        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = "DELETE FROM HashCache WHERE Path = @path";
        cmd.Parameters.AddWithValue("@path", path.ToString().ToLowerInvariant());
        cmd.PrepareAsync();

        cmd.ExecuteNonQuery();
    }

    private async Task Upsert(AbsolutePath path, long lastModified, Hash hash, long size)
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

    /// <summary>
    ///     Fills in the size of a row written before the column existed, so it gains the same protection as a
    ///     freshly written one without the file being read again.
    /// </summary>
    private async Task BackfillSize(AbsolutePath path, long size)
    {
        await using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = "UPDATE HashCache SET Size = @size WHERE Path = @path AND Size IS NULL";
        cmd.Parameters.AddWithValue("@size", size);
        cmd.Parameters.AddWithValue("@path", path.ToString().ToLowerInvariant());
        await cmd.PrepareAsync();

        await cmd.ExecuteNonQueryAsync();
    }

    public void VacuumDatabase()
    {
        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"VACUUM";
        cmd.PrepareAsync();

        cmd.ExecuteNonQuery();
    }

    public async Task<Hash> TryGetHashCache(AbsolutePath file)
    {
        if (!file.FileExists()) return default;

        var result = await Get(file);
        if (result == default || result.Hash == default)
            return default;
        
        // Fix for strange issue where dates are messed up on some systems
        if (file.LastModifiedUtc() < file.CreatedUtc())
            file.Touch();

        if (result.LastModified != file.LastModifiedUtc().ToFileTimeUtc())
        {
            Purge(file);
            return default;
        }

        // A file damaged in place often keeps its timestamp, so the timestamp alone does not establish that
        // the cached hash still describes the file. Size is cheap to check and catches truncation.
        var size = file.Size();

        if (result.Size == null)
        {
            // Written before the column existed. Trusted as it always has been, and given a size now so that
            // later damage is caught.
            await BackfillSize(file, size);
            return result.Hash;
        }

        if (result.Size != size)
        {
            Purge(file);
            return default;
        }

        return result.Hash;
    }

    private async Task WriteHashCache(AbsolutePath file, Hash hash)
    {
        if (!file.FileExists()) return;
        await Upsert(file, file.LastModifiedUtc().ToFileTimeUtc(), hash, file.Size());
    }

    public async Task FileHashWriteCache(AbsolutePath file, Hash hash)
    {
        await WriteHashCache(file, hash);
    }

    public async Task<Hash> FileHashCachedAsync(AbsolutePath file, CancellationToken token)
    {
        var hash = await TryGetHashCache(file);
        if (hash != default) return hash;

        using var job = await _limiter.Begin($"Hashing {file.FileName}", file.Size(), token);
        await using var fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

        hash = await fs.HashingCopy(Stream.Null, token, job);
        if (hash != default)
            await WriteHashCache(file, hash);
        return hash;
    }
}