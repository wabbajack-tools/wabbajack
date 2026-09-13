using System;
using System.Collections.Immutable;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs.Streams;
using Wabbajack.DTOs.Vfs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.VFS.Interfaces;

namespace Wabbajack.VFS;

public class VFSDiskCache : IVfsCache
{
    private readonly SQLiteConnection _conn;
    private readonly string _connectionString;
    private readonly AbsolutePath _path;

    /// <summary>
    ///     One connection, one statement at a time. Archives are analyzed in parallel and each of them reads
    ///     and writes through this connection while an AddRoots finishing elsewhere runs <see cref="Clean" />,
    ///     a VACUUM that cannot start while another statement on the connection is still open; it fails with
    ///     "SQL logic error". The lock has to cover every statement rather than only the vacuum, because what
    ///     a vacuum needs is for nothing else to be in flight.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    public VFSDiskCache(AbsolutePath path)
    {
        _path = path;

        if (!_path.Parent.DirectoryExists())
            _path.Parent.CreateDirectory();

        _connectionString = string.Intern($"URI=file:{path};Pooling=True;Max Pool Size=100; Journal Mode=Memory;");
        _conn = new SQLiteConnection(_connectionString);
        _conn.Open();

        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS VFSCache (
            Hash BIGINT PRIMARY KEY,
            Contents BLOB)
            WITHOUT ROWID";
        cmd.ExecuteNonQuery();
    }

    public async Task<IndexedVirtualFile?> Get(Hash hash, IStreamFactory sfn, CancellationToken token)
    {
        if (hash == default)
            throw new ArgumentException("Cannot cache default hashes");

        byte[]? contents = null;

        await _lock.WaitAsync(token);
        try
        {
            await using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"SELECT Contents FROM VFSCache WHERE Hash = @hash";
            cmd.Parameters.AddWithValue("@hash", (long) hash);

            await using var rdr = await cmd.ExecuteReaderAsync(token);
            if (await rdr.ReadAsync(token))
                contents = (byte[]) rdr.GetValue(0);
        }
        finally
        {
            _lock.Release();
        }

        if (contents == null) return null;

        // Unpacked after the lock is released. The row is a gzipped tree of every file in the archive, and
        // indexing reads this cache from every thread at once; decompressing under the lock would serialize
        // the one part of the read that does not need the connection. Put compresses outside it for the same
        // reason.
        await using var ms = new MemoryStream(contents);
        return IndexedVirtualFileExtensions.Read(ms);
    }
    
    public async Task Put(IndexedVirtualFile ivf, CancellationToken token)
    {
        await using var ms = new MemoryStream();
        // Top level path gets renamed when read, we don't want the absolute path
        // here else the reader will blow up when it tries to convert the value
        ivf.Name = (RelativePath) "not/applicable";
        ivf.Write(ms);
        ms.Position = 0;
        await InsertIntoVFSCache(ivf.Hash, ms);
    }

    public async Task Clean()
    {
        await _lock.WaitAsync();
        try
        {
            await using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"VACUUM";
            await cmd.PrepareAsync();

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task InsertIntoVFSCache(Hash hash, MemoryStream data)
    {
        await _lock.WaitAsync();
        try
        {
            await using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"INSERT INTO VFSCache (Hash, Contents) VALUES (@hash, @contents)";
            cmd.Parameters.AddWithValue("@hash", (long) hash);
            var val = new SQLiteParameter("@contents", DbType.Binary) {Value = data.ToArray()};
            cmd.Parameters.Add(val);
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SQLiteException ex)
            {
                if (ex.Message.StartsWith("constraint failed"))
                    return;
                throw;
            }
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
}