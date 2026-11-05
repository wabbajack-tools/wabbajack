using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Linq;
using System.Text;

namespace Wabbajack.Common;

/// <summary>
///     The FileVersion and ProductVersion strings of a Windows executable, as Explorer's Details tab shows them.
///     On Windows these come from <see cref="FileVersionInfo" />. Elsewhere .NET reads version information only
///     from managed assemblies, so <c>SkyrimSE.exe</c> would have none; there the version resource is read straight
///     out of the PE file.
/// </summary>
public sealed record ExecutableVersion(string? FileVersion, string? ProductVersion)
{
    private const int RtVersion = 16;
    private const uint FixedFileInfoSignature = 0xFEEF04BD;

    public static ExecutableVersion Read(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new ExecutableVersion(info.FileVersion, info.ProductVersion);
        }

        return ReadPortable(path);
    }

    /// <summary>The same answer from the PE file alone, on any platform. Nulls when there is no version resource.</summary>
    public static ExecutableVersion ReadPortable(string path)
    {
        try
        {
            var blob = VersionResource(path);
            if (blob == null) return new ExecutableVersion(null, null);

            var fixedVersions = FixedVersions(blob);
            return new ExecutableVersion(StringValue(blob, "FileVersion") ?? fixedVersions.File,
                StringValue(blob, "ProductVersion") ?? fixedVersions.Product);
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException or InvalidOperationException
                                       or ArgumentOutOfRangeException)
        {
            return new ExecutableVersion(null, null);
        }
    }

    /// <summary>The bytes of the first RT_VERSION resource: VS_VERSIONINFO.</summary>
    private static byte[]? VersionResource(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var directory = pe.PEHeaders.PEHeader?.ResourceTableDirectory;
        if (directory is not {Size: > 0} dir) return null;

        var resources = pe.GetSectionData(dir.RelativeVirtualAddress).GetContent().ToArray();

        // Type, then name, then language: the version is under type 16, and the first entry of each level below.
        var names = FindEntry(resources, 0, RtVersion);
        if (names is not { } namesOffset || !IsDirectory(namesOffset)) return null;
        var languages = FirstEntry(resources, Offset(namesOffset));
        if (languages is not { } languagesOffset || !IsDirectory(languagesOffset)) return null;
        var data = FirstEntry(resources, Offset(languagesOffset));
        if (data is not { } dataOffset || IsDirectory(dataOffset)) return null;

        var entry = (int) dataOffset;
        var rva = BitConverter.ToInt32(resources, entry);
        var size = BitConverter.ToInt32(resources, entry + 4);
        var content = pe.GetSectionData(rva).GetContent().ToArray();
        return content.AsSpan(0, Math.Min(size, content.Length)).ToArray();
    }

    private static bool IsDirectory(uint offsetToData) => (offsetToData & 0x80000000) != 0;
    private static int Offset(uint offsetToData) => (int) (offsetToData & 0x7FFFFFFF);

    /// <summary>OffsetToData of the entry with this integer id in the directory at <paramref name="directory" />.</summary>
    private static uint? FindEntry(byte[] resources, int directory, int id)
    {
        var named = BitConverter.ToUInt16(resources, directory + 12);
        var ids = BitConverter.ToUInt16(resources, directory + 14);
        for (var i = named; i < named + ids; i++)
        {
            var entry = directory + 16 + i * 8;
            if (BitConverter.ToUInt32(resources, entry) == id) return BitConverter.ToUInt32(resources, entry + 4);
        }

        return null;
    }

    private static uint? FirstEntry(byte[] resources, int directory)
    {
        var count = BitConverter.ToUInt16(resources, directory + 12) + BitConverter.ToUInt16(resources, directory + 14);
        return count == 0 ? null : BitConverter.ToUInt32(resources, directory + 16 + 4);
    }

    /// <summary>VS_FIXEDFILEINFO's two versions, as a.b.c.d.</summary>
    private static (string? File, string? Product) FixedVersions(byte[] blob)
    {
        for (var i = 0; i + 52 <= blob.Length; i += 4)
        {
            if (BitConverter.ToUInt32(blob, i) != FixedFileInfoSignature) continue;
            return (Dotted(BitConverter.ToUInt32(blob, i + 8), BitConverter.ToUInt32(blob, i + 12)),
                Dotted(BitConverter.ToUInt32(blob, i + 16), BitConverter.ToUInt32(blob, i + 20)));
        }

        return (null, null);
    }

    private static string Dotted(uint ms, uint ls) => $"{ms >> 16}.{ms & 0xFFFF}.{ls >> 16}.{ls & 0xFFFF}";

    /// <summary>
    ///     A value from StringFileInfo. Each String is a header, its key in UTF-16 with a terminator, padding to a
    ///     four-byte boundary, then the value in UTF-16.
    /// </summary>
    private static string? StringValue(byte[] blob, string key)
    {
        var pattern = Encoding.Unicode.GetBytes(key + "\0");
        for (var at = blob.AsSpan().IndexOf(pattern); at >= 0;)
        {
            // A String's key sits six bytes into it, so only a match at an even offset with room for the header
            // in front is one; anything else is the same characters somewhere else.
            if (at >= 6 && at % 2 == 0)
            {
                var valueLength = BitConverter.ToUInt16(blob, at - 4);
                var start = (at + pattern.Length + 3) & ~3;
                if (valueLength == 0) return null;
                var end = start;
                while (end + 1 < blob.Length && (blob[end] != 0 || blob[end + 1] != 0)) end += 2;
                var value = Encoding.Unicode.GetString(blob, start, end - start).Trim();
                return value.Length == 0 ? null : value;
            }

            var next = blob.AsSpan(at + 1).IndexOf(pattern);
            at = next < 0 ? -1 : at + 1 + next;
        }

        return null;
    }
}
