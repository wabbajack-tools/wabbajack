using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Compression.BSA
{
    public static class Utils
    {
        private static Encoding Windows1251 = Encoding.GetEncoding(1251);
        public static string ReadStringLen(this BinaryReader rdr)
        {
            var len = rdr.ReadByte();
            var bytes = rdr.ReadBytes(len - 1);
            rdr.ReadByte();
            return Windows1251.GetString(bytes);
        }

        public static string ReadStringTerm(this BinaryReader rdr)
        {
            List<byte> acc = new List<byte>();
            while (true)
            {
                var c = rdr.ReadByte();

                if (c == '\0') break;

                acc.Add(c);
            }
            return Windows1251.GetString(acc.ToArray());
        }


        /// <summary>
        /// Returns bytes for a \0 terminated string
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public static byte[] ToBZString(this string val)
        {
            var b = Windows1251.GetBytes(val);
            var b2 = new byte[b.Length + 2];
            b.CopyTo(b2, 1);
            b2[0] = (byte)(b.Length + 1);
            return b2;
        }

        /// <summary>
        /// Returns bytes for unterminated string with a count at the start
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public static byte[] ToBSString(this string val)
        {
            var b = Windows1251.GetBytes(val);
            var b2 = new byte[b.Length + 1];
            b.CopyTo(b2, 1);
            b2[0] = (byte)b.Length;

            return b2;
        }

        /// <summary>
        /// Returns bytes for a \0 terminated string prefixed by a length
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public static byte[] ToTermString(this string val)
        {
            var b = Windows1251.GetBytes(val);
            var b2 = new byte[b.Length + 1];
            b.CopyTo(b2, 0);
            b[0] = (byte)b.Length;
            return b2;
        }

        public static ulong GetBSAHash(this string name)
        {
            name = name.Replace('/', '\\');
            return GetBSAHash(Path.ChangeExtension(name, null), Path.GetExtension(name));
        }

        private static ulong GetBSAHash(string name, string ext)
        {
            name = name.ToLowerInvariant();
            ext = ext.ToLowerInvariant();
            var hashBytes = new byte[]
            {
                (byte)(name.Length == 0 ? '\0' : name[name.Length - 1]),
                (byte)(name.Length < 3 ? '\0' : name[name.Length - 2]),
                (byte)name.Length,
                (byte)name[0]
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
            for (var i = 1; i < name.Length - 2; i++)
            {
                hash2 = hash2 * 0x1003f + (byte)name[i];
            }

            uint hash3 = 0;
            for (var i = 0; i < ext.Length; i++)
            {
                hash3 = hash3 * 0x1003f + (byte)ext[i];
            }

            return (((ulong)(hash2 + hash3)) << 32) + hash1;
        }

        public static void CopyToLimit(this Stream frm, Stream tw, int limit)
        {
            byte[] buff = new byte[1024];
            while (limit > 0)
            {
                int to_read = Math.Min(buff.Length, limit);
                int read = frm.Read(buff, 0, to_read);
                tw.Write(buff, 0, read);
                limit -= read;
            }
        }
    }
}

