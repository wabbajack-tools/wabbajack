using System;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public static class PatchCache
    {
        private static AbsolutePath DBLocation = Consts.LocalAppDataPath.Combine("GlobalPatchCache.sqlite");
        private static string _connectionString;
        private static SQLiteConnection _conn;

        static PatchCache()
        {
            _connectionString = String.Intern($"URI=file:{DBLocation};Pooling=True;Max Pool Size=100; Journal Mode=Memory;");
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

        public static async Task CreatePatchCached(byte[] a, byte[] b, Stream output)
        {
            await using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"INSERT INTO PatchCache (FromHash, ToHash, PatchSize, Patch) 
                  VALUES (@fromHash, @toHash, @patchSize, @patch)";

            var dataA = a.xxHash();
            var dataB = b.xxHash();

            cmd.Parameters.AddWithValue("@fromHash", (long)dataA);
            cmd.Parameters.AddWithValue("@toHash", (long)dataB);
            
            await using var patch = new MemoryStream();

            Utils.Status("Creating Patch");
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

        public static async Task<long> CreatePatchCached(Stream srcStream, Hash srcHash, Stream destStream, Hash destHash,
            Stream? patchOutStream = null)
        {
            if (patchOutStream == null)
            {
                await using var rcmd = new SQLiteCommand(_conn);
                rcmd.CommandText = "SELECT PatchSize FROM PatchCache WHERE FromHash = @fromHash AND ToHash = @toHash";
                rcmd.Parameters.AddWithValue("@fromHash", (long)srcHash);
                rcmd.Parameters.AddWithValue("@toHash", (long)destHash);

                await using var rdr = await rcmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    return rdr.GetInt64(0);
                }
            }
            else
            {

                if (TryGetPatch(srcHash, destHash, out var entry))
                {
                    await patchOutStream!.WriteAsync(await entry.GetData());
                    return entry.PatchSize;
                }
            }

            await using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"INSERT INTO PatchCache (FromHash, ToHash, PatchSize, Patch) 
                  VALUES (@fromHash, @toHash, @patchSize, @patch)";

            cmd.Parameters.AddWithValue("@fromHash", (long)srcHash);
            cmd.Parameters.AddWithValue("@toHash", (long)destHash);

            Utils.Status("Creating Patch");
            await using var sigStream = new MemoryStream();
            await using var patchStream = new MemoryStream();
            OctoDiff.Create(srcStream, destStream, sigStream, patchStream);

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

            if (patchOutStream == null) return patchStream.Position;

            patchStream.Position = 0;
            await patchStream.CopyToAsync(patchOutStream);

            return patchStream.Position;
        }

        public static bool TryGetPatch(Hash fromHash, Hash toHash, [MaybeNullWhen(false)] out CacheEntry found)
        {
            using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"SELECT PatchSize FROM PatchCache WHERE FromHash = @fromHash AND ToHash = @toHash";
            cmd.Parameters.AddWithValue("@fromHash", (long)fromHash);
            cmd.Parameters.AddWithValue("@toHash", (long)toHash);

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                found = new CacheEntry(fromHash, toHash, rdr.GetInt64(0));
                return true;
            }

            found = default;
            return false;
        }

        public record CacheEntry(Hash From, Hash To, long PatchSize)
        {
            public async Task<byte[]> GetData()
            {
                await using var cmd = new SQLiteCommand(_conn);
                cmd.CommandText = @"SELECT PatchSize, Patch FROM PatchCache WHERE FromHash = @fromHash AND ToHash = @toHash";
                cmd.Parameters.AddWithValue("@fromHash", (long)From);
                cmd.Parameters.AddWithValue("@toHash", (long)To);

                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var array = new byte[rdr.GetInt64(0)];
                    rdr.GetBytes(1, 0, array, 0, array.Length);
                    return array;
                }
                
                return Array.Empty<byte>();
            }
        }

        public static void VacuumDatabase()
        {
            using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"VACUUM";
            cmd.PrepareAsync();

            cmd.ExecuteNonQuery();
        }

        public static void ApplyPatch(Stream input, Func<Stream> openPatchStream, Stream output)
        {
            using var ps = openPatchStream();
            using var br = new BinaryReader(ps);
            var bytes = br.ReadBytes(8);
            var str = Encoding.ASCII.GetString(bytes);
            switch (str)
            {
                case "BSDIFF40":
                    BSDiff.Apply(input, openPatchStream, output);
                    return;
                case "OCTODELT":
                    OctoDiff.Apply(input, openPatchStream, output);
                    return;
                default:
                    throw new Exception($"No diff dispatch for: {str}");
            }
        }
    }

    // Convenience hook ins to offer the API from Utils, without having the init fire until they're actually called
    public static partial class Utils
    {
        public static void ApplyPatch(Stream input, Func<Stream> openPatchStream, Stream output) =>
            PatchCache.ApplyPatch(input, openPatchStream, output);

        public static Task CreatePatchCached(byte[] a, byte[] b, Stream output) =>
            PatchCache.CreatePatchCached(a, b, output);

        public static Task<long> CreatePatchCached(Stream srcStream, Hash srcHash, Stream destStream, Hash destHash, Stream? patchOutStream = null) =>
            PatchCache.CreatePatchCached(srcStream, srcHash, destStream, destHash, patchOutStream);

        public static bool TryGetPatch(Hash foundHash, Hash fileHash, [MaybeNullWhen(false)] out PatchCache.CacheEntry ePatch) =>
            PatchCache.TryGetPatch(foundHash, fileHash, out ePatch);
    }
}
