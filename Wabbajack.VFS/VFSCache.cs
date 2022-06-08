using System;
using System.Collections.Immutable;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs.Streams;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.VFS;

public class VFSCache
{
    private readonly SQLiteConnection _conn;
    private readonly string _connectionString;
    private readonly AbsolutePath _path;

    public VFSCache(AbsolutePath path)
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

    public bool TryGetFromCache(Context context, VirtualFile parent, IPath path, IStreamFactory extractedFile,
        Hash hash, out VirtualFile found)
    {
        if (hash == default)
            throw new ArgumentException("Cannot cache default hashes");
        
        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"SELECT Contents FROM VFSCache WHERE Hash = @hash";
        cmd.Parameters.AddWithValue("@hash", (long) hash);

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            var data = IndexedVirtualFile.Read(rdr.GetStream(0));
            found = ConvertFromIndexedFile(context, data, path, parent, extractedFile);
            found.Name = path;
            found.Hash = hash;
            return true;
        }

        found = default;
        return false;
    }

    private static VirtualFile ConvertFromIndexedFile(Context context, IndexedVirtualFile file, IPath path,
        VirtualFile vparent, IStreamFactory extractedFile)
    {
        var vself = new VirtualFile
        {
            Context = context,
            Name = path,
            Parent = vparent,
            Size = file.Size,
            LastModified = extractedFile.LastModifiedUtc.AsUnixTime(),
            LastAnalyzed = DateTime.Now.AsUnixTime(),
            Hash = file.Hash,
            ImageState = file.ImageState
        };

        vself.FillFullPath();

        vself.Children = file.Children.Select(f => ConvertFromIndexedFile(context, f, f.Name, vself, extractedFile))
            .ToImmutableList();

        return vself;
    }

    public async Task WriteToCache(VirtualFile self)
    {
        await using var ms = new MemoryStream();
        var ivf = self.ToIndexedVirtualFile();
        // Top level path gets renamed when read, we don't want the absolute path
        // here else the reader will blow up when it tries to convert the value
        ivf.Name = (RelativePath) "not/applicable";
        ivf.Write(ms);
        ms.Position = 0;
        await InsertIntoVFSCache(self.Hash, ms);
    }

    private async Task InsertIntoVFSCache(Hash hash, MemoryStream data)
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

    public void VacuumDatabase()
    {
        using var cmd = new SQLiteCommand(_conn);
        cmd.CommandText = @"VACUUM";
        cmd.PrepareAsync();

        cmd.ExecuteNonQuery();
    }
}