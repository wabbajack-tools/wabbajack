using System;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
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
    private readonly ILogger<BinaryPatchCache> _logger;

    public BinaryPatchCache(ILogger<BinaryPatchCache> logger, AbsolutePath location)
    {
        _logger = logger;
        _location = location;
        if (!_location.DirectoryExists())
            _location.CreateDirectory();
    }

    public AbsolutePath PatchLocation(Hash srcHash, Hash destHash)
    {
        return _location.Combine($"{srcHash.ToHex()}_{destHash.ToHex()}.octodiff");
    }

    public async Task<CacheEntry> CreatePatch(Stream srcStream, Hash srcHash, Stream destStream, Hash destHash, IJob? job)
    {

        var location = PatchLocation(srcHash, destHash);
        if (location.FileExists())
            return new CacheEntry(srcHash, destHash, location.Size(), this);
        
        await using var sigStream = new MemoryStream();
        var tempName = _location.Combine(Guid.NewGuid().ToString()).WithExtension(Ext.Temp);
        await using var patchStream = tempName.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        try
        {

            OctoDiff.Create(srcStream, destStream, sigStream, patchStream, job);

            patchStream.Close();
            await tempName.MoveToAsync(location, true, CancellationToken.None);
        }
        finally
        {
            await patchStream.DisposeAsync();
            if (tempName.FileExists())
                tempName.Delete();
        }

        return new CacheEntry(srcHash, destHash, location.Size(), this);
    }


    public async Task<CacheEntry?> GetPatch(Hash fromHash, Hash toHash)
    {
        var location = PatchLocation(fromHash, toHash);
        if (location.FileExists())
            return new CacheEntry(fromHash, toHash, location.Size(), this);
        return null;
    }

    public async Task<byte[]> GetData(CacheEntry entry)
    {
        var location = PatchLocation(entry.From, entry.To);
        if (location.FileExists())
            return await location.ReadAllBytesAsync();
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