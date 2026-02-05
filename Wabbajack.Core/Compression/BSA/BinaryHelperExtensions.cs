using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Wabbajack.Common;
using Wabbajack.Paths;
#pragma warning disable SYSLIB0001

namespace Wabbajack.Compression.BSA;

public static class BinaryHelperExtensions
{
    private static readonly Encoding Windows1252;

    static BinaryHelperExtensions()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1252 = Encoding.GetEncoding(1252);
    }

    private static Encoding GetEncoding(VersionType version)
    {
        return version switch
        {
            VersionType.TES3 => Encoding.ASCII,
            VersionType.FO3 => Encoding.UTF8,
            VersionType.SSE => Windows1252,
            _ => Encoding.UTF7
        };
    }

    public static string ReadStringLen(this BinaryReader rdr, VersionType version)
    {
        var len = rdr.ReadByte();
        if (len == 0) return string.Empty;

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
    ///     Returns \0 terminated bytes for a string encoded with a given BSA version's encoding format
    /// </summary>
    /// <param name="val"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    public static byte[] ToBZString(this RelativePath val, VersionType version)
    {
        var b = GetEncoding(version).GetBytes((string) val);
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
    public static byte[] ToBSString(this RelativePath val)
    {
        var b = Encoding.ASCII.GetBytes((string) val);
        var b2 = new byte[b.Length + 1];
        b.CopyTo(b2, 1);
        b2[0] = (byte) b.Length;

        return b2;
    }

    public static string ReadStringLenTerm(this ReadOnlyMemorySlice<byte> bytes, VersionType version)
    {
        if (bytes.Length <= 1) return string.Empty;
        return GetEncoding(version).GetString(bytes.Slice(1, bytes[0]));
    }

    public static string ReadStringTerm(this ReadOnlyMemorySlice<byte> bytes, VersionType version)
    {
        if (bytes.Length <= 1) return string.Empty;
        return GetEncoding(version).GetString(bytes[..^1]);
    }

    /// <summary>
    ///     Returns bytes for a string with a length prefix, version is the BSA version
    /// </summary>
    /// <param name="val"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    public static byte[] ToTermString(this string val, VersionType version)
    {
        var b = GetEncoding(version).GetBytes(val);
        var b2 = new byte[b.Length + 1];
        b.CopyTo(b2, 0);
        b[0] = (byte) b.Length;
        return b2;
    }

    public static byte[] ToTermString(this RelativePath val, VersionType version)
    {
        return ((string) val).ToTermString(version);
    }

    public static ulong GetBSAHash(this string name)
    {
        name = name.Replace('/', '\\');
        return GetBSAHash(Path.ChangeExtension(name, null), Path.GetExtension(name));
    }

    public static ulong GetBSAHash(this RelativePath name)
    {
        return ((string) name).GetBSAHash();
    }

    public static ulong GetFolderBSAHash(this RelativePath name)
    {
        return GetBSAHash((string) name, "");
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
}