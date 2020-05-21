using System;
using System.Collections.Generic;
using System.Data.HashFunction.xxHash;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RocksDbSharp;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

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

        public string ToHex()
        {
            return BitConverter.GetBytes(_code).ToHex();
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

        public byte[] ToArray()
        {
            return BitConverter.GetBytes(_code);
        }
    }
    
    public static partial class Utils
    {
        private static RocksDb _hashCache;

        public static Hash ReadHash(this BinaryReader br)
        {
            return new Hash(br.ReadUInt64());
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
            return sha.Hash.ToHex();
        }

        public static Hash FileHash(this AbsolutePath file, bool nullOnIoError = false)
        {
            try
            {
                using var fs = file.OpenRead();
                var config = new xxHashConfig {HashSizeInBits = 64};
                using var f = new StatusFileStream(fs, $"Hashing {(string)file.FileName}");
                return new Hash(BitConverter.ToUInt64(xxHashFactory.Instance.Create(config).ComputeHash(f).Hash));
            }
            catch (IOException)
            {
                if (nullOnIoError) return Hash.Empty;
                throw;
            }
        }
        public static Hash xxHash(this byte[] data)
        {
            var hash = new xxHashConfig();
            hash.HashSizeInBits = 64;
            hash.Seed = 0x42;
            using var fs = new MemoryStream(data);
            var config = new xxHashConfig {HashSizeInBits = 64};
            using var f = new StatusFileStream(fs, $"Hashing memory stream");
            var value = xxHashFactory.Instance.Create(config).ComputeHash(f);
            return Hash.FromULong(BitConverter.ToUInt64(value.Hash));
        }
        
        public static Hash xxHash(this Stream stream)
        {
            var hash = new xxHashConfig();
            hash.HashSizeInBits = 64;
            hash.Seed = 0x42;
            var config = new xxHashConfig {HashSizeInBits = 64};
            using var f = new StatusFileStream(stream, $"Hashing memory stream");
            var value = xxHashFactory.Instance.Create(config).ComputeHash(f);
            return Hash.FromULong(BitConverter.ToUInt64(value.Hash));
        }
        public static Hash FileHashCached(this AbsolutePath file, bool nullOnIoError = false)
        {
            if (TryGetHashCache(file, out var foundHash)) return foundHash;

            var hash = file.FileHash(nullOnIoError);
            if (hash != Hash.Empty) 
                WriteHashCache(file, hash);
            return hash;
        }

        public static bool TryGetHashCache(AbsolutePath file, out Hash hash)
        {
            var normPath = Encoding.UTF8.GetBytes(file.Normalize());
            var value = _hashCache.Get(normPath);
            hash = default;
            
            if (value == null) return false;
            if (value.Length != 20) return false;

            using var ms = new MemoryStream(value);
            using var br = new BinaryReader(ms);
            var version = br.ReadUInt32();
            if (version != HashCacheVersion) return false;

            var lastModified = br.ReadUInt64();
            if (lastModified != file.LastModifiedUtc.AsUnixTime()) return false;
            hash = new Hash(br.ReadUInt64());
            return true;
        }


        private const uint HashCacheVersion = 0x01;
        private static void WriteHashCache(AbsolutePath file, Hash hash)
        {
            using var ms = new MemoryStream(20);
            using var bw = new BinaryWriter(ms);
            bw.Write(HashCacheVersion);
            var lastModified = file.LastModifiedUtc.AsUnixTime();
            bw.Write(lastModified);
            bw.Write((ulong)hash);
            _hashCache.Put(Encoding.UTF8.GetBytes(file.Normalize()), ms.ToArray());
        }

        public static async Task<Hash> FileHashCachedAsync(this AbsolutePath file, bool nullOnIOError = false)
        {
            if (TryGetHashCache(file, out var foundHash)) return foundHash;

            var hash = await file.FileHashAsync(nullOnIOError);
            if (hash != Hash.Empty) 
                WriteHashCache(file, hash);
            return hash;
        }

        public static async Task<Hash> FileHashAsync(this AbsolutePath file, bool nullOnIOError = false)
        {
            try
            {
                await using var fs = file.OpenRead();
                var config = new xxHashConfig {HashSizeInBits = 64};
                await using var hs = new StatusFileStream(fs, $"Hashing {file}");
                var value = await xxHashFactory.Instance.Create(config).ComputeHashAsync(hs);
                return new Hash(BitConverter.ToUInt64(value.Hash));
            }
            catch (IOException)
            {
                if (nullOnIOError) return Hash.Empty;
                throw;
            }
        }

    }
}
