using System.Buffers.Binary;
using System.Text;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Networking.Bethesda;

/// <summary>
///     The <c>.ckm</c> container a Creation is delivered in. A four-byte <c>BTAR</c> magic, a
///     <c>u16</c> version, a <c>u16</c> flags field, then a run of files, each of them a <c>u16</c> name
///     length, the name, a <c>u64</c> size and that many bytes. No compression, no index, no terminator:
///     the file ends when the bytes run out.
///     <para>
///         Everything this reads came off the network, including the names, so it is written to refuse
///         rather than to cope. The magic is checked, every length is checked against what is actually left
///         in the buffer before it is used, a name is reduced to its leaf so that no entry can name a path
///         out of the folder it is being written to, and running off the end is an exception rather than a
///         short read. The alternative - trusting a length - is a container that decides how much memory to
///         allocate or which file on disk to overwrite.
///     </para>
/// </summary>
public static class BtarArchive
{
    /// <summary>The four bytes a container starts with.</summary>
    public static ReadOnlySpan<byte> Magic => "BTAR"u8;

    /// <summary>Magic, version and flags. Entries start here.</summary>
    private const int HeaderSize = 8;

    /// <summary>
    ///     Reads the container's table of contents. The returned data slices point into
    ///     <paramref name="container" />; nothing is copied.
    /// </summary>
    /// <exception cref="BtarFormatException">The container is not a BTAR, or is malformed or truncated.</exception>
    public static BtarContents Read(ReadOnlyMemory<byte> container)
    {
        var span = container.Span;
        if (span.Length < HeaderSize)
            throw new BtarFormatException(
                $"Container is {span.Length} bytes, too short to hold even a BTAR header ({HeaderSize} bytes).");

        if (!span[..4].SequenceEqual(Magic))
            throw new BtarFormatException($"Not a BTAR container; magic is {Describe(span[..4])}.");

        var version = BinaryPrimitives.ReadUInt16LittleEndian(span[4..]);
        var flags = BinaryPrimitives.ReadUInt16LittleEndian(span[6..]);

        var entries = new List<BtarEntry>();
        var offset = HeaderSize;

        while (offset < span.Length)
        {
            var remaining = span.Length - offset;
            if (remaining < sizeof(ushort))
                throw new BtarFormatException(
                    $"Container ends mid-entry at offset {offset}: {remaining} byte(s) left, a name length needs {sizeof(ushort)}.");

            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]);
            offset += sizeof(ushort);

            if (nameLength == 0)
                throw new BtarFormatException($"Entry at offset {offset - sizeof(ushort)} has an empty name.");

            if (span.Length - offset < nameLength)
                throw new BtarFormatException(
                    $"Entry name at offset {offset} claims {nameLength} bytes but only {span.Length - offset} remain.");

            // Latin-1 rather than UTF-8: the names are ASCII in every container seen, and a decoder that
            // cannot fail keeps a stray high byte from turning into a replacement character that then
            // silently fails to match a file name.
            var rawName = Encoding.Latin1.GetString(span.Slice(offset, nameLength));
            offset += nameLength;

            if (span.Length - offset < sizeof(ulong))
                throw new BtarFormatException(
                    $"Entry '{rawName}' has no size field: {span.Length - offset} byte(s) left, a size needs {sizeof(ulong)}.");

            var size = BinaryPrimitives.ReadUInt64LittleEndian(span[offset..]);
            offset += sizeof(ulong);

            var left = (ulong) (span.Length - offset);
            if (size > left)
                throw new BtarFormatException(
                    $"Entry '{rawName}' claims {size} bytes but only {left} remain in the container.");

            entries.Add(new BtarEntry(SafeName(rawName), rawName, container.Slice(offset, (int) size)));
            offset += (int) size;
        }

        if (entries.Count == 0)
            throw new BtarFormatException("Container holds no entries.");

        return new BtarContents(version, flags, entries);
    }

    /// <summary>
    ///     Reads the container and writes every entry into <paramref name="folder" /> under its leaf name.
    ///     Nothing is written until the whole container has parsed, so a truncated one leaves no files
    ///     behind.
    /// </summary>
    public static async Task<IReadOnlyList<RelativePath>> ExtractTo(ReadOnlyMemory<byte> container,
        AbsolutePath folder, CancellationToken token = default)
    {
        var contents = Read(container);
        folder.CreateDirectory();

        var written = new List<RelativePath>(contents.Entries.Count);
        foreach (var entry in contents.Entries)
        {
            var name = entry.Name.ToRelativePath();
            await folder.Combine(name).WriteAllBytesAsync(Writable(entry.Data), token);
            written.Add(name);
        }

        return written;
    }

    /// <summary>
    ///     The name an entry is written under: its leaf, with both separators honoured because the
    ///     containers write Windows ones (<c>data\Foo.bsa</c>) and a hostile one could write either. A leaf
    ///     of <c>.</c> or <c>..</c>, or one that is only separators, is refused rather than quietly
    ///     rewritten - there is no sensible file name to fall back to, and inventing one would write
    ///     somebody's payload under a name they did not choose.
    /// </summary>
    public static string SafeName(string rawName)
    {
        var span = rawName.AsSpan();
        var cut = span.LastIndexOfAny('\\', '/');
        var leaf = (cut < 0 ? span : span[(cut + 1)..]).ToString();

        if (leaf.Length == 0)
            throw new BtarFormatException($"Entry name '{rawName}' has no file name in it.");
        if (leaf is "." or "..")
            throw new BtarFormatException($"Entry name '{rawName}' is a directory reference, not a file.");
        if (leaf.Contains(':'))
            throw new BtarFormatException($"Entry name '{rawName}' names a drive or stream.");
        if (leaf.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new BtarFormatException($"Entry name '{rawName}' is not a usable file name.");

        return leaf;
    }

    private static Memory<byte> Writable(ReadOnlyMemory<byte> data)
    {
        // WriteAllBytesAsync wants a writable Memory; the slice is read-only by construction, so copy.
        var buffer = new byte[data.Length];
        data.CopyTo(buffer);
        return buffer;
    }

    private static string Describe(ReadOnlySpan<byte> bytes)
    {
        return $"0x{Convert.ToHexString(bytes)}";
    }
}

/// <summary>What a container turned out to hold.</summary>
/// <param name="Version">The <c>u16</c> after the magic. Recorded, not acted on.</param>
/// <param name="Flags">The <c>u16</c> after that. Recorded, not acted on.</param>
/// <param name="Entries">The files, in the order the container lists them.</param>
public sealed record BtarContents(ushort Version, ushort Flags, IReadOnlyList<BtarEntry> Entries);

/// <summary>One file inside a container.</summary>
/// <param name="Name">The leaf name it is safe to write, which is the only one anything here uses.</param>
/// <param name="RawName">The name exactly as the container wrote it, for messages.</param>
/// <param name="Data">The file's bytes, a slice of the container.</param>
public sealed record BtarEntry(string Name, string RawName, ReadOnlyMemory<byte> Data);

/// <summary>A container that is not a BTAR, or is one that cannot be believed.</summary>
public class BtarFormatException : Exception
{
    public BtarFormatException(string message) : base(message)
    {
    }
}
