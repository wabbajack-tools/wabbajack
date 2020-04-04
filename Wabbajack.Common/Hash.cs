using System;
using System.Collections.Generic;
using System.Data.HashFunction.xxHash;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Common
{
    
    /// <summary>
    /// Struct representing a xxHash64 value. It's a struct with a ulong in it, but wrapped so we don't confuse
    /// it with other longs in the system.
    /// </summary>
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
    }
    
    public static partial class Utils
    {
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
            var hashFile = file.WithExtension(Consts.HashFileExtension);
            hash = Hash.Empty; 
            if (!hashFile.IsFile) return false;
            
            if (hashFile.Size != 20) return false;

            using var fs = hashFile.OpenRead();
            using var br = new BinaryReader(fs);
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
            using var fs = file.WithExtension(Consts.HashFileExtension).Create();
            using var bw = new BinaryWriter(fs);
            bw.Write(HashCacheVersion);
            var lastModified = file.LastModifiedUtc.AsUnixTime();
            bw.Write(lastModified);
            bw.Write((ulong)hash);
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
                var value = await xxHashFactory.Instance.Create(config).ComputeHashAsync(fs);
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
