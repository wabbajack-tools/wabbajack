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
    }

    private (AbsolutePath Path, long LastModified, Hash Hash) Get(AbsolutePath path)
    {
        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = "SELECT LastModified, Hash FROM HashCache WHERE Path = @path";
        cmd.Parameters.AddWithValue("@path", path.ToString());
        cmd.PrepareAsync();

        using var reader = cmd.ExecuteReader();
        while (reader.Read()) return (path, reader.GetInt64(0), Hash.FromLong(reader.GetInt64(1)));

        return default;
    }

    public void Purge(AbsolutePath path)
    {
        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = "DELETE FROM HashCache WHERE Path = @path";
        cmd.Parameters.AddWithValue("@path", path.ToString());
        cmd.PrepareAsync();

        cmd.ExecuteNonQuery();
    }

    private void Upsert(AbsolutePath path, long lastModified, Hash hash)
    {
        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"INSERT INTO HashCache (Path, LastModified, Hash) VALUES (@path, @lastModified, @hash)
            ON CONFLICT(Path) DO UPDATE SET LastModified = @lastModified, Hash = @hash";
        cmd.Parameters.AddWithValue("@path", path.ToString());
        cmd.Parameters.AddWithValue("@lastModified", lastModified);
        cmd.Parameters.AddWithValue("@hash", (long) hash);
        cmd.PrepareAsync();

        cmd.ExecuteNonQuery();
    }

    public void VacuumDatabase()
    {
        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"VACUUM";
        cmd.PrepareAsync();

        cmd.ExecuteNonQuery();
    }

    public bool TryGetHashCache(AbsolutePath file, out Hash hash)
    {
        hash = default;
        if (!file.FileExists()) return false;

        var result = Get(file);
        if (result == default || result.Hash == default)
            return false;

        if (result.LastModified == file.LastModifiedUtc().ToFileTimeUtc())
        {
            hash = result.Hash;
            return true;
        }

        Purge(file);
        return false;
    }

    private void WriteHashCache(AbsolutePath file, Hash hash)
    {
        if (!file.FileExists()) return;
        Upsert(file, file.LastModifiedUtc().ToFileTimeUtc(), hash);
    }

    public void FileHashWriteCache(AbsolutePath file, Hash hash)
    {
        WriteHashCache(file, hash);
    }

    public async Task<Hash> FileHashCachedAsync(AbsolutePath file, CancellationToken token)
    {
        if (TryGetHashCache(file, out var foundHash)) return foundHash;

        using var job = await _limiter.Begin($"Hashing {file.FileName}", file.Size(), token);
        await using var fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

        var hash = await fs.HashingCopy(Stream.Null, token, job);
        if (hash != default)
            WriteHashCache(file, hash);
        return hash;
    }
}