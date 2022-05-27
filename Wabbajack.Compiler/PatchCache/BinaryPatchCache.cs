using System;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Compiler.PatchCache;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Compiler;

public class BinaryPatchCache : IBinaryPatchCache
{
    private readonly SQLiteConnection _conn;
    private readonly string _connectionString;
    private readonly AbsolutePath _location;

    public BinaryPatchCache(AbsolutePath location)
    {
        _location = location;
        if (!_location.Parent.DirectoryExists())
            _location.Parent.CreateDirectory();

        _connectionString =
            string.Intern($"URI=file:{location.ToString()};Pooling=True;Max Pool Size=100; Journal Mode=Memory;");
        _conn = new SQLiteConnection(_connectionString);
        _conn.Open();

        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS PatchCache (
            FromHash BIGINT,
            ToHash BIGINT,
            PatchSize BLOB,
            Patch BLOB,
            PRIMARY KEY (FromHash, ToHash))
            WITHOUT ROWID;";

        cmd.ExecuteNonQuery();
    }

    public async Task<CacheEntry> CreatePatch(Stream srcStream, Hash srcHash, Stream destStream, Hash destHash, IJob? job)
    {
        await using var rcmd = new SQLiteCommand(_conn);
        rcmd.CommandText = "SELECT PatchSize FROM PatchCache WHERE FromHash = @fromHash AND ToHash = @toHash";
        rcmd.Parameters.AddWithValue("@fromHash", (long) srcHash);
        rcmd.Parameters.AddWithValue("@toHash", (long) destHash);

        await using var rdr = await rcmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync()) return new CacheEntry(srcHash, destHash, rdr.GetInt64(0), this);

        await using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"INSERT INTO PatchCache (FromHash, ToHash, PatchSize, Patch) 
                  VALUES (@fromHash, @toHash, @patchSize, @patch)";

        cmd.Parameters.AddWithValue("@fromHash", (long) srcHash);
        cmd.Parameters.AddWithValue("@toHash", (long) destHash);

        await using var sigStream = new MemoryStream();
        await using var patchStream = new MemoryStream();
        OctoDiff.Create(srcStream, destStream, sigStream, patchStream, job);

        cmd.Parameters.AddWithValue("@patchSize", patchStream.Length);
        cmd.Parameters.AddWithValue("@patch", patchStream.ToArray());
        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SQLiteException ex)
        {
            if (!ex.Message.StartsWith("constraint failed"))
                throw;
        }

        return new CacheEntry(srcHash, destHash, patchStream.Length, this);
    }


    public async Task<CacheEntry?> GetPatch(Hash fromHash, Hash toHash)
    {
        await using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"SELECT PatchSize FROM PatchCache WHERE FromHash = @fromHash AND ToHash = @toHash";
        cmd.Parameters.AddWithValue("@fromHash", (long) fromHash);
        cmd.Parameters.AddWithValue("@toHash", (long) toHash);

        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync()) return new CacheEntry(fromHash, toHash, rdr.GetInt64(0), this);
        return null;
    }

    public async Task<byte[]> GetData(CacheEntry entry)
    {
        await using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"SELECT PatchSize, Patch FROM PatchCache WHERE FromHash = @fromHash AND ToHash = @toHash";
        cmd.Parameters.AddWithValue("@fromHash", (long) entry.From);
        cmd.Parameters.AddWithValue("@toHash", (long) entry.To);

        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            var array = new byte[rdr.GetInt64(0)];
            rdr.GetBytes(1, 0, array, 0, array.Length);
            return array;
        }

        return Array.Empty<byte>();
    }

    public async Task CreatePatchCached(byte[] a, byte[] b, Stream output)
    {
        await using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"INSERT INTO PatchCache (FromHash, ToHash, PatchSize, Patch) 
                  VALUES (@fromHash, @toHash, @patchSize, @patch)";

        xxHashAlgorithm aAl = new(0), bAl = new(0);

        var dataA = Hash.FromULong(aAl.HashBytes(a));
        ;
        var dataB = Hash.FromULong(bAl.HashBytes(b));
        ;

        cmd.Parameters.AddWithValue("@fromHash", (long) dataA);
        cmd.Parameters.AddWithValue("@toHash", (long) dataB);

        await using var patch = new MemoryStream();
        OctoDiff.Create(a, b, patch);
        patch.Position = 0;

        cmd.Parameters.AddWithValue("@patchSize", patch.Length);
        cmd.Parameters.AddWithValue("@patch", patch.ToArray());
        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SQLiteException ex)
        {
            if (!ex.Message.StartsWith("constraint failed"))
                throw;
        }

        await patch.CopyToAsync(output);
    }
}