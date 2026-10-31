using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     Writes a BTAR container the way the server does, so the reader can be fed a real one and then the
///     same one with a single field spoiled. Everything is settable on purpose - a builder that could only
///     produce valid containers would be no use for the half of these tests that matter.
/// </summary>
public sealed class BtarBuilder
{
    private readonly List<(string Name, byte[] Data, ulong? DeclaredSize)> _entries = new();

    public byte[] Magic { get; init; } = Encoding.ASCII.GetBytes("BTAR");
    public ushort Version { get; init; } = 1;
    public ushort Flags { get; init; }

    /// <summary>Bytes to lop off the end once the container is written, to make a truncated one.</summary>
    public int Truncate { get; init; }

    /// <summary>
    ///     Adds a file. <paramref name="declaredSize" /> overrides the size field so a container can claim
    ///     more bytes than it carries.
    /// </summary>
    public BtarBuilder With(string name, byte[] data, ulong? declaredSize = null)
    {
        _entries.Add((name, data, declaredSize));
        return this;
    }

    public BtarBuilder With(string name, string text)
    {
        return With(name, Encoding.ASCII.GetBytes(text));
    }

    public byte[] Build()
    {
        using var stream = new MemoryStream();
        stream.Write(Magic);

        var header = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(header, Version);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2), Flags);
        stream.Write(header);

        foreach (var (name, data, declaredSize) in _entries)
        {
            var nameBytes = Encoding.Latin1.GetBytes(name);
            var nameLength = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(nameLength, (ushort) nameBytes.Length);
            stream.Write(nameLength);
            stream.Write(nameBytes);

            var size = new byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(size, declaredSize ?? (ulong) data.Length);
            stream.Write(size);
            stream.Write(data);
        }

        var bytes = stream.ToArray();
        return Truncate <= 0 ? bytes : bytes[..^Truncate];
    }
}
