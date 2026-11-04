using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Wabbajack.Compression.BSA;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler.Test.Offline;

/// <summary>
///     Builds the mod archive the compiler tests install, in place of the one they used to download. It has the
///     same shape: a plugin, an uncompressed meshes BSA and a compressed textures BSA, the textures BSA the
///     smaller of the two, holding two large textures whose names start with "mrkinn" that the texture tests
///     recompress. Everything is generated from a fixed seed, so the archive and its hashes are the same on
///     every run and nothing has to be checked in.
/// </summary>
public static class GeneratedFixture
{
    public const string Name = "Generated Fixture";

    /// <summary>Files in the textures BSA, which is the one the tests extract.</summary>
    public const int TextureBsaFiles = 36;

    public const int MeshBsaFiles = 156;

    public static string FileName => Name + ".zip";

    /// <summary>
    ///     The archive's bytes. Built once per test run: every harness gets the same archive, and the fake
    ///     download server answers with it too.
    /// </summary>
    public static Task<byte[]> Bytes => _bytes.Value;

    private static readonly Lazy<Task<byte[]>> _bytes = new(Build);

    public static async Task<AbsolutePath> Create(AbsolutePath folder)
    {
        var zip = folder.Combine(FileName);
        await zip.WriteAllBytesAsync(await Bytes);
        return zip;
    }

    private static async Task<byte[]> Build()
    {
        var random = new Random(1234);
        await using var manager = new TemporaryFileManager(KnownFolders.EntryPoint.Combine("generated_fixture_" + Guid.NewGuid()));
        var staging = manager.CreateFolder().Path;

        await staging.Combine(Name + ".esp").WriteAllBytesAsync(RandomBytes(random, 64 * 1024));
        await WriteMeshBsa(staging.Combine(Name + ".bsa"), random, manager);
        await WriteTextureBsa(staging.Combine(Name + " - Textures.bsa"), random, manager);

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var file in staging.EnumerateFiles().OrderBy(f => f.FileName.ToString()))
            {
                var entry = archive.CreateEntry(file.FileName.ToString());
                entry.LastWriteTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
                await using var target = entry.Open();
                await using var source = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                await source.CopyToAsync(target);
            }
        return ms.ToArray();
    }

    private static async Task WriteMeshBsa(AbsolutePath path, Random random, TemporaryFileManager manager)
    {
        // Uncompressed, as the original's meshes archive was.
        var state = new BSAState {Magic = "BSA\0", Version = 105, ArchiveFlags = 0x3, FileFlags = 0};
        await using var builder = BSADispatch.CreateBuilder(state, manager);
        for (var i = 0; i < MeshBsaFiles; i++)
        {
            var file = new BSAFile
            {
                Index = i,
                Path = $@"meshes\generated\set{i % 11}\mesh{i}.nif".ToRelativePath()
            };
            await builder.AddFile(file, new MemoryStream(RandomBytes(random, random.Next(8, 24) * 1024)),
                CancellationToken.None);
        }

        await using var fs = path.Open(FileMode.Create, FileAccess.Write);
        await builder.Build(fs, CancellationToken.None);
    }

    private static async Task WriteTextureBsa(AbsolutePath path, Random random, TemporaryFileManager manager)
    {
        // Compressed, as the original's textures archive was.
        var state = new BSAState {Magic = "BSA\0", Version = 105, ArchiveFlags = 0x7, FileFlags = 0};
        await using var builder = BSADispatch.CreateBuilder(state, manager);
        for (var i = 0; i < TextureBsaFiles; i++)
        {
            // The first two are the large textures the texture tests look for by name.
            var (name, size) = i < 2 ? ($"mrkinn_{(char) ('a' + i)}.dds", 1024) : ($"detail{i}.dds", 64);
            var file = new BSAFile
            {
                Index = i,
                Path = $@"textures\generated\set{i % 5}\{name}".ToRelativePath()
            };
            await builder.AddFile(file, new MemoryStream(Texture(random, size)), CancellationToken.None);
        }

        await using var fs = path.Open(FileMode.Create, FileAccess.Write);
        await builder.Build(fs, CancellationToken.None);
    }

    /// <summary>
    ///     A BC1 DDS with a full mip chain: smooth gradients and a few discs, so it has structure for the
    ///     perceptual hash to find again after it is resized and recompressed.
    /// </summary>
    private static byte[] Texture(Random random, int size)
    {
        using var image = new Image<Rgba32>(size, size);
        var (r, g, b) = (random.Next(256), random.Next(256), random.Next(256));
        var discs = new (int X, int Y, int R)[4];
        for (var d = 0; d < discs.Length; d++)
            discs[d] = (random.Next(size), random.Next(size), random.Next(size / 16, size / 4));

        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < size; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < size; x++)
                {
                    var inDisc = Array.Exists(discs, c => (x - c.X) * (x - c.X) + (y - c.Y) * (y - c.Y) < c.R * c.R);
                    row[x] = inDisc
                        ? new Rgba32((byte) (255 - r), (byte) (255 - g), (byte) (255 - b), 255)
                        : new Rgba32((byte) ((r + x * 255 / size) / 2), (byte) ((g + y * 255 / size) / 2), (byte) b, 255);
                }
            }
        });

        var encoder = new BcEncoder
        {
            OutputOptions = {Format = CompressionFormat.Bc1, GenerateMipMaps = true, FileFormat = OutputFileFormat.Dds}
        };
        using var ms = new MemoryStream();
        encoder.EncodeToDds(image).Write(ms);
        return ms.ToArray();
    }

    private static byte[] RandomBytes(Random random, int count)
    {
        var bytes = new byte[count];
        random.NextBytes(bytes);
        return bytes;
    }
}
