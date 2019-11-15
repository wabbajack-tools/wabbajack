using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Compression.BSA
{
    internal static class Utils
    {
        private static readonly Encoding Windows1252 = Encoding.GetEncoding(1252);

        private static Encoding GetEncoding(VersionType version)
        {
            if (version == VersionType.SSE)
                return Windows1252;
            return Encoding.UTF7;
        }

        public static string ReadStringLen(this BinaryReader rdr, VersionType version)
        {
            var len = rdr.ReadByte();
            if (len == 0)
                //rdr.ReadByte();
                return "";

            var bytes = rdr.ReadBytes(len - 1);
            rdr.ReadByte();
            return GetEncoding(version).GetString(bytes);
        }

        public static string ReadStringLenNoTerm(this BinaryReader rdr, VersionType version)
        {
            var len = rdr.ReadByte();
            var bytes = rdr.ReadBytes(len);
            return GetEncoding(version).GetString(bytes);
        }

        public static string ReadStringTerm(this BinaryReader rdr, VersionType version)
        {
            var acc = new List<byte>();
            while (true)
            {
                var c = rdr.ReadByte();

                if (c == '\0') break;

                acc.Add(c);
            }

            return GetEncoding(version).GetString(acc.ToArray());
        }

        /// <summary>
        ///     Returns bytes for a \0 terminated string
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public static byte[] ToBZString(this string val, VersionType version)
        {
            var b = GetEncoding(version).GetBytes(val);
            var b2 = new byte[b.Length + 2];
            b.CopyTo(b2, 1);
            b2[0] = (byte) (b.Length + 1);
            return b2;
        }

        /// <summary>
        ///     Returns bytes for unterminated string with a count at the start
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public static byte[] ToBSString(this string val)
        {
            var b = Encoding.ASCII.GetBytes(val);
            var b2 = new byte[b.Length + 1];
            b.CopyTo(b2, 1);
            b2[0] = (byte) b.Length;

            return b2;
        }

        /// <summary>
        ///     Returns bytes for a \0 terminated string prefixed by a length
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public static byte[] ToTermString(this string val, VersionType version)
        {
            var b = GetEncoding(version).GetBytes(val);
            var b2 = new byte[b.Length + 1];
            b.CopyTo(b2, 0);
            b[0] = (byte) b.Length;
            return b2;
        }

        public static ulong GetBSAHash(this string name)
        {
            name = name.Replace('/', '\\');
            return GetBSAHash(Path.ChangeExtension(name, null), Path.GetExtension(name));
        }

        public static ulong GetBSAHash(this string name, string ext)
        {
            name = name.ToLowerInvariant();
            ext = ext.ToLowerInvariant();

            if (string.IsNullOrEmpty(name))
                return 0;

            var hashBytes = new[]
            {
                (byte) (name.Length == 0 ? '\0' : name[name.Length - 1]),
                (byte) (name.Length < 3 ? '\0' : name[name.Length - 2]),
                (byte) name.Length,
                (byte) name[0]
            };
            var hash1 = BitConverter.ToUInt32(hashBytes, 0);
            switch (ext)
            {
                case ".kf":
                    hash1 |= 0x80;
                    break;
                case ".nif":
                    hash1 |= 0x8000;
                    break;
                case ".dds":
                    hash1 |= 0x8080;
                    break;
                case ".wav":
                    hash1 |= 0x80000000;
                    break;
            }

            uint hash2 = 0;
            for (var i = 1; i < name.Length - 2; i++) hash2 = hash2 * 0x1003f + (byte) name[i];

            uint hash3 = 0;
            for (var i = 0; i < ext.Length; i++) hash3 = hash3 * 0x1003f + (byte) ext[i];

            return ((ulong) (hash2 + hash3) << 32) + hash1;
        }

        public static async Task CopyToLimitAsync(this Stream frm, Stream tw, int limit)
        {
            var buff = new byte[1024 * 16];
            while (limit > 0)
            {
                var to_read = Math.Min(buff.Length, limit);
                var read = await frm.ReadAsync(buff, 0, to_read);
                await tw.WriteAsync(buff, 0, read);
                limit -= read;
            }

            await tw.FlushAsync();
        }

        public static void CopyToLimit(this Stream frm, Stream tw, int limit)
        {
            var buff = new byte[1024];
            while (limit > 0)
            {
                var to_read = Math.Min(buff.Length, limit);
                var read = frm.Read(buff, 0, to_read);
                tw.Write(buff, 0, read);
                limit -= read;
            }

            tw.Flush();
        }
    }
}