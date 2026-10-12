using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Compression.BSA;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.FileExtractor.Test.Offline;

/// <summary>
///     Writes small archives of each format the extractor reads, at test time, so nothing binary is checked
///     in and every test knows exactly what is inside what it opens. Entry names use backslashes, the way the
///     extractor reports them.
/// </summary>
public static class TestArchives
{
    public static readonly (string Name, string Content)[] Entries =
    {
        (@"meshes\armor\cuirass.nif", "cuirass"),
        (@"textures\armor\cuirass.dds", "cuirass texture"),
        ("readme.txt", "read me")
    };

    public static async Task<AbsolutePath> Zip(AbsolutePath path, params (string Name, string Content)[] entries)
    {
        await using (var stream = path.Open(FileMode.Create, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                await using var entryStream = archive.CreateEntry(name.Replace('\\', '/')).Open();
                await entryStream.WriteAsync(Encoding.UTF8.GetBytes(content));
            }
        }

        return path;
    }

    /// <summary>A Skyrim SE BSA; <paramref name="compressed" /> sets the archive's compression flag.</summary>
    public static async Task<AbsolutePath> Bsa(AbsolutePath path, TemporaryFileManager manager, bool compressed,
        params (string Name, string Content)[] entries)
    {
        var state = new BSAState {Magic = "BSA\0", Version = 105, ArchiveFlags = compressed ? 0x7u : 0x3u};
        await using var builder = BSADispatch.CreateBuilder(state, manager);
        for (var i = 0; i < entries.Length; i++)
            await builder.AddFile(new BSAFile {Index = i, Path = entries[i].Name.ToRelativePath()},
                new MemoryStream(Encoding.UTF8.GetBytes(entries[i].Content)), CancellationToken.None);

        await using var fs = path.Open(FileMode.Create, FileAccess.Write);
        await builder.Build(fs, CancellationToken.None);
        return path;
    }

    /// <summary>A Fallout 4 general BA2 with a name table.</summary>
    public static async Task<AbsolutePath> Ba2(AbsolutePath path, TemporaryFileManager manager,
        params (string Name, string Content)[] entries)
    {
        var state = new BA2State {HeaderMagic = "BTDX", Version = 1, Type = BA2EntryType.GNRL, HasNameTable = true};
        await using var builder = BSADispatch.CreateBuilder(state, manager);
        for (var i = 0; i < entries.Length; i++)
        {
            var extension = Path.GetExtension(entries[i].Name).TrimStart('.').PadRight(4, '\0')[..4];
            await builder.AddFile(new BA2File
                {
                    Index = i,
                    Path = entries[i].Name.ToRelativePath(),
                    Extension = extension,
                    Align = 0,
                    Flags = 0x00100100
                },
                new MemoryStream(Encoding.UTF8.GetBytes(entries[i].Content)), CancellationToken.None);
        }

        await using var fs = path.Open(FileMode.Create, FileAccess.Write);
        await builder.Build(fs, CancellationToken.None);
        return path;
    }

    /// <summary>
    ///     A Morrowind BSA. The format records every offset itself, so they are worked out here: the 12 byte
    ///     header, then sizes and offsets, name offsets, names, the hash table, and the data.
    /// </summary>
    public static async Task<AbsolutePath> MorrowindBsa(AbsolutePath path, params (string Name, string Content)[] entries)
    {
        var data = entries.Select(e => Encoding.UTF8.GetBytes(e.Content)).ToArray();
        var files = new List<TES3File>();
        uint nameOffset = 0, dataOffset = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            files.Add(new TES3File
            {
                Index = i,
                Path = entries[i].Name.ToRelativePath(),
                NameOffset = nameOffset,
                Offset = dataOffset,
                Size = (uint) data[i].Length,
                Hash1 = (uint) i,
                Hash2 = 0
            });
            nameOffset += (uint) Encoding.ASCII.GetByteCount(entries[i].Name) + 1;
            dataOffset += (uint) data[i].Length;
        }

        var count = (uint) entries.Length;
        var hashOffset = count * 8 + count * 4 + nameOffset;
        var state = new TES3State
        {
            VersionNumber = 0x100,
            FileCount = count,
            HashOffset = hashOffset,
            DataOffset = 12 + hashOffset + count * 8
        };

        await using var builder = BSADispatch.CreateBuilder(state, null!);
        foreach (var file in files)
            await builder.AddFile(file, new MemoryStream(data[file.Index]), CancellationToken.None);

        await using var fs = path.Open(FileMode.Create, FileAccess.ReadWrite);
        await builder.Build(fs, CancellationToken.None);
        return path;
    }

    /// <summary>A BTAR container, the format a Bethesda Creation arrives in.</summary>
    public static async Task<AbsolutePath> Btar(AbsolutePath path, ushort major, ushort minor,
        params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        stream.Write("BTAR"u8);
        var header = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(header, major);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2), minor);
        stream.Write(header);

        foreach (var (name, content) in entries)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            var lengths = new byte[2 + 8];
            BinaryPrimitives.WriteUInt16LittleEndian(lengths, (ushort) nameBytes.Length);
            stream.Write(lengths, 0, 2);
            stream.Write(nameBytes);
            var bytes = Encoding.UTF8.GetBytes(content);
            BinaryPrimitives.WriteUInt64LittleEndian(lengths.AsSpan(2), (ulong) bytes.Length);
            stream.Write(lengths, 2, 8);
            stream.Write(bytes);
        }

        await path.WriteAllBytesAsync(stream.ToArray());
        return path;
    }
}
