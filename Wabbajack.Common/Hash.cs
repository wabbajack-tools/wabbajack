using System;
using System.Collections.Generic;
using System.Data;
using System.Data.HashFunction.xxHash;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
using System.Data.SQLite;

namespace Wabbajack.Common
{
    
    /// <summary>
    /// Struct representing a xxHash64 value. It's a struct with a ulong in it, but wrapped so we don't confuse
    /// it with other longs in the system.
    /// </summary>
    [JsonConverter(typeof(Utils.HashJsonConverter))]
    public struct Hash
    {
        private readonly ulong _code;
        public Hash(ulong code = 0)
        {
            _code = code;
        }

        
        public bool IsValid => _code != 0;

        public override string ToString()
        {
            return BitConverter.GetBytes(_code).ToBase64();
        }

        public override bool Equals(object? obj)
        {
            if (obj is Hash h)
                return h._code == _code;
            return false;
        }

        public override int GetHashCode()
        {
            return (int)(_code >> 32) ^ (int)_code;
        }

        public static bool operator ==(Hash a, Hash b)
        {
            return a._code == b._code;
        }

        public static bool operator !=(Hash a, Hash b)
        {
            return !(a == b);
        }
        
        public static explicit operator ulong(Hash a)
        {
            return a._code;
        }
        
        public static explicit operator long(Hash a)
        {
            return BitConverter.ToInt64(BitConverter.GetBytes(a._code));
        }



        public string ToBase64()
        {
            return BitConverter.GetBytes(_code).ToBase64();
        }

        public static Hash FromBase64(string hash)
        {
            return new Hash(BitConverter.ToUInt64(hash.FromBase64()));
        }

        public static Hash Empty = new Hash();

        public static Hash FromLong(in long argHash)
        {
            return new Hash(BitConverter.ToUInt64(BitConverter.GetBytes(argHash)));
        }
        
        public static Hash FromULong(in ulong argHash)
        {
            return new Hash(argHash);
        }

        public static Hash FromHex(string xxHashAsHex)
        {
            return new Hash(BitConverter.ToUInt64(xxHashAsHex.FromHex()));
        }
        public string ToHex()
        {
            return BitConverter.GetBytes(_code).ToHex();
        }

        public byte[] ToArray()
        {
            return BitConverter.GetBytes(_code);
        }
        
        public static Hash Interpret(string input)
        {
            return input.Length switch
            {
                16 => FromHex(input),
                12 when input.EndsWith('=') => FromBase64(input),
                _ => FromLong(long.Parse(input))
            };
        }
    }

    public static class HashCache
    {
        private static AbsolutePath DBLocation = Consts.LocalAppDataPath.Combine("GlobalHashCache.sqlite");
        private static string _connectionString;
        private static SQLiteConnection _conn;


        // Keep rock DB out of Utils, as it causes lock problems for users of Wabbajack.Common that aren't interested in it, otherwise

        static HashCache()
        {
            _connectionString = String.Intern($"URI=file:{DBLocation};Pooling=True;Max Pool Size=100; Journal Mode=Memory;");
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

        private static (AbsolutePath Path, long LastModified, Hash Hash) GetFromCache(AbsolutePath path)
        {
            using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = "SELECT LastModified, Hash FROM HashCache WHERE Path = @path";
            cmd.Parameters.AddWithValue("@path", path.ToString());
            cmd.PrepareAsync();
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                return (path, reader.GetInt64(0), Hash.FromLong(reader.GetInt64(1)));
            }

            return default;
        }
        
        private static void PurgeCacheEntry(AbsolutePath path)
        {
            using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = "DELETE FROM HashCache WHERE Path = @path";
            cmd.Parameters.AddWithValue("@path", path.ToString());
            cmd.PrepareAsync();
            
            cmd.ExecuteNonQuery();
        }

        private static void UpsertCacheEntry(AbsolutePath path, long lastModified, Hash hash)
        {
            using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"INSERT INTO HashCache (Path, LastModified, Hash) VALUES (@path, @lastModified, @hash)
            ON CONFLICT(Path) DO UPDATE SET LastModified = @lastModified, Hash = @hash";
            cmd.Parameters.AddWithValue("@path", path.ToString());
            cmd.Parameters.AddWithValue("@lastModified", lastModified);
            cmd.Parameters.AddWithValue("@hash", (long)hash);
            cmd.PrepareAsync();
            
            cmd.ExecuteNonQuery();
        }

        public static void VacuumDatabase()
        {
            using var cmd = new SQLiteCommand(_conn);
            cmd.CommandText = @"VACUUM";
            cmd.PrepareAsync();

            cmd.ExecuteNonQuery();
        }

        public static Hash ReadHash(this BinaryReader br)
        {
            return new(br.ReadUInt64());
        }

        public static void Write(this BinaryWriter bw, Hash hash)
        {
            bw.Write((ulong)hash);
        }

        public static string StringSha256Hex(this string s)
        {
            var sha = new SHA256Managed();
            using (var o = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
            {
                using var i = new MemoryStream(Encoding.UTF8.GetBytes(s));
                i.CopyTo(o);
            }
            return sha.Hash!.ToHex();
        }

        public static Hash xxHash(this byte[] data)
        {
            var hash = new xxHashConfig();
            hash.HashSizeInBits = 64;
            hash.Seed = 0x42;
            using var fs = new MemoryStream(data);
            var config = new xxHashConfig { HashSizeInBits = 64 };
            using var f = new StatusFileStream(fs, $"Hashing memory stream");
            var value = xxHashFactory.Instance.Create(config).ComputeHash(f);
            return Hash.FromULong(BitConverter.ToUInt64(value.Hash));
        }

        public static Hash xxHash(this Stream stream)
        {
            var hash = new xxHashConfig();
            hash.HashSizeInBits = 64;
            hash.Seed = 0x42;
            var config = new xxHashConfig { HashSizeInBits = 64 };
            using var f = new StatusFileStream(stream, $"Hashing memory stream");
            var value = xxHashFactory.Instance.Create(config).ComputeHash(f);
            return Hash.FromULong(BitConverter.ToUInt64(value.Hash));
        }

        public static async Task<Hash> xxHashAsync(this Stream stream)
        {
            var config = new xxHashConfig { HashSizeInBits = 64 };
            await using var f = new StatusFileStream(stream, $"Hashing memory stream");
            var value = await xxHashFactory.Instance.Create(config).ComputeHashAsync(f);
            return Hash.FromULong(BitConverter.ToUInt64(value.Hash));
        }

        public static bool TryGetHashCache(this AbsolutePath file, out Hash hash)
        {
            hash = default;
            if (!file.Exists) return false;

            var result = GetFromCache(file);
            if (result == default)
                return false;

            if (result.LastModified == file.LastModifiedUtc.ToFileTimeUtc())
            {
                hash = result.Hash;
                return true;
            }

            PurgeCacheEntry(file);
            return false;
        }

        private static void WriteHashCache(this AbsolutePath file, Hash hash)
        {
            if (!file.Exists) return;
            UpsertCacheEntry(file, file.LastModifiedUtc.ToFileTimeUtc(), hash);
        }

        public static void FileHashWriteCache(this AbsolutePath file, Hash hash)
        {
            WriteHashCache(file, hash);
        }

        public static async Task<Hash?> FileHashCachedAsync(this AbsolutePath file)
        {
            if (TryGetHashCache(file, out var foundHash))
            {
                if (foundHash != default) 
                    return foundHash;
            }

            var hash = await file.FileHashAsync();
            if (hash != null && hash != Hash.Empty)
                WriteHashCache(file, hash.Value);
            return hash;
        }

        public static async Task<Hash?> FileHashAsync(this AbsolutePath file)
        {
            try
            {
                await using var fs = await file.OpenRead();
                await using var bs = new BufferedStream(fs, 1024 * 1024 * 10);
                var config = new xxHashConfig { HashSizeInBits = 64 };
                await using var hs = new StatusFileStream(fs, $"Hashing {file}");
                var value = await xxHashFactory.Instance.Create(config).ComputeHashAsync(hs);
                return new Hash(BitConverter.ToUInt64(value.Hash));
            }
            catch (IOException e)
            {
                Utils.Error(e, $"Unable to hash file {file}");
                return null;
            }
        }
    }
}
