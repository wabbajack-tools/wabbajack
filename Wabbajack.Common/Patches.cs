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

        static PatchCache()
        {
            _connectionString = String.Intern($"URI=file:{DBLocation};Pooling=True;Max Pool Size=100;");
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();

            using var cmd = new SQLiteCommand(conn);
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS PatchCache (
            FromHash BIGINT,
            ToHash BIGINT,
            PatchSize BLOB,
            Patch BLOB,
            PRIMARY KEY (FromHash, ToHash))";
            cmd.ExecuteNonQuery();

        }

        public static async Task CreatePatchCached(byte[] a, byte[] b, Stream output)
        {
            await using var conn = new SQLiteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SQLiteCommand(conn);
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
                if (!ex.Message.StartsWith("constraint exception"))
                    throw;
            }
            await patch.CopyToAsync(output);
        }

        public static async Task<long> CreatePatchCached(Stream srcStream, Hash srcHash, Stream destStream, Hash destHash,
            Stream? patchOutStream = null)
        {
            await using var conn = new SQLiteConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SQLiteCommand(conn);
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
                if (!ex.Message.StartsWith("constraint exception"))
                    throw;

            }

            if (patchOutStream == null) return patchStream.Position;

            patchStream.Position = 0;
            await patchStream.CopyToAsync(patchOutStream);

            return patchStream.Position;
        }

        public static bool TryGetPatch(Hash fromHash, Hash toHash, [MaybeNullWhen(false)] out byte[] array)
        {
            using var conn = new SQLiteConnection(_connectionString);
            conn.Open();

            using var cmd = new SQLiteCommand(conn);
            cmd.CommandText = @"SELECT PatchSize, Patch FROM PatchCache WHERE FromHash = @fromHash AND ToHash = @toHash";
            cmd.Parameters.AddWithValue("@fromHash", (long)fromHash);
            cmd.Parameters.AddWithValue("@toHash", (long)toHash);

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                array = new byte[rdr.GetInt64(0)];
                rdr.GetBytes(1, 0, array, 0, array.Length);
                return true;
            }

            array = Array.Empty<byte>();
            return false;




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

        public static bool TryGetPatch(Hash foundHash, Hash fileHash, [MaybeNullWhen(false)] out byte[] ePatch) =>
            PatchCache.TryGetPatch(foundHash, fileHash, out ePatch);
    }
}
