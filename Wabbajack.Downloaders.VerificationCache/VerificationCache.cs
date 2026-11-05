using System.Data.SQLite;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Downloaders.VerificationCache;

public class VerificationCache : IVerificationCache, IDisposable
{
    private readonly AbsolutePath _location;
    private readonly string _connectionString;
    private readonly SQLiteConnection _conn;
    private readonly TimeSpan _expiry;
    private readonly ILogger<VerificationCache> _logger;
    private readonly DTOSerializer _dtos;

    public VerificationCache(ILogger<VerificationCache> logger, AbsolutePath location, TimeSpan expiry, DTOSerializer dtos)
    {
        _logger = logger;
        _location = location;
        _expiry = expiry;
        _dtos = dtos;

        if (!_location.Parent.DirectoryExists())
            _location.Parent.CreateDirectory();

        _connectionString =
            string.Intern($"URI=file:{_location};Pooling=True;Max Pool Size=100;");
        _conn = new SQLiteConnection(_connectionString);
        _conn.Open();
        // The cache is shared by every Wabbajack process the user runs, so wait for another one's write rather
        // than failing on the spot, which is SQLite's default. The journal mode is set here rather than in the
        // connection string because Open applies that before any busy timeout, and fails just the same.
        // WAL with synchronous=NORMAL, because in the default journal every commit waits for a disk flush
        // (about 7 ms here), and an install commits one row per file it writes.
        using (var pragmas = new SQLiteCommand("PRAGMA busy_timeout=60000; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;", _conn))
            pragmas.ExecuteNonQuery();


        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS VerficationCache (
            PKS TEXT PRIMARY KEY,
            LastModified BIGINT,
            State TEXT)
            WITHOUT ROWID";
        cmd.ExecuteNonQuery();
    }

    public async Task<(bool? IsValid, IDownloadState? State)> Get(IDownloadState archive)
    {
        var key = archive.PrimaryKeyString;

        await using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = "SELECT LastModified, State FROM VerficationCache WHERE PKS = @pks";
        cmd.Parameters.AddWithValue("@pks", key);
        await cmd.PrepareAsync();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var ts = DateTime.FromFileTimeUtc(reader.GetInt64(0));
            var state = JsonSerializer.Deserialize<IDownloadState>(reader.GetString(1), _dtos.Options);
            
            return (DateTime.UtcNow - ts <= _expiry, state);
        }

        return (null, null);
    }

    public async Task Put(IDownloadState state, bool valid)
    {
        var key = state.PrimaryKeyString;
        if (valid)
        {
            await using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"INSERT INTO VerficationCache (PKS, LastModified, State) VALUES (@pks, @lastModified, @state)
            ON CONFLICT(PKS) DO UPDATE SET LastModified = @lastModified, State = @state";
            cmd.Parameters.AddWithValue("@pks", key);
            cmd.Parameters.AddWithValue("@lastModified", DateTime.UtcNow.ToFileTimeUtc());
            cmd.Parameters.AddWithValue("@state", JsonSerializer.Serialize(state, _dtos.Options));
            await cmd.PrepareAsync();

            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            _logger.LogInformation("Marking {Key} as invalid", key);
            await using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"DELETE FROM VerficationCache WHERE PKS = @pks";
            cmd.Parameters.AddWithValue("@pks", state.PrimaryKeyString);
            await cmd.PrepareAsync();
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public void Dispose()
    {
        _conn.Close();
        _conn.Dispose();
    }
}