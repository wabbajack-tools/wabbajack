using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Security.Cryptography;
using SteamKit2;
using SteamKit2.CDN;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     Steam does not compress every depot chunk the same way, and which way it chose is not in the
///     manifest -- it is four magic bytes inside the chunk, readable only once the depot key has decrypted
///     it. Deflate in a zip container is the old one; LZMA behind a <c>VZa</c> header is the one before
///     last; Zstd behind <c>VSZa</c> is what Valve's build tools emit now.
///     <para>
///         Depots are rebuilt a chunk at a time, so one depot serves a mixture. Skyrim Special Edition's
///         own depots, untouched for years, are still zip throughout, while its Creation Kit -- app
///         1946180, built much more recently -- answers with both: <c>Tools\AssetWatcher\Qt5Gui.dll</c> in
///         zip and <c>Tools\AssetWatcher\Qt5Core.dll</c>, in the same folder of the same depot, in Zstd.
///         A client that does not know the third format does not fail on a whole depot, or on large files,
///         or on any category a reader of the log could name. It fails on scattered individual files, and
///         it fails by handing Zstd bytes to a zip reader, which says "End of Central Directory record
///         could not be found" -- a sentence about a zip, for something that was never one.
///     </para>
///     <para>
///         That is exactly what a repair of the Creation Kit did: 42 chunk fetches lost to a decompressor
///         chosen by elimination rather than by magic bytes. So this pins the capability rather than any
///         code of ours, because the fix was the dependency: a chunk of each format that Steam can send has
///         to survive the round trip through <see cref="DepotChunk.Process" /> exactly as the CDN client
///         hands it over.
///     </para>
/// </summary>
public class DepotChunkCompressionTests
{
    /// <summary>Any 32 bytes will do; the test encrypts with the same key it then decrypts with.</summary>
    private static readonly byte[] DepotKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>
    ///     The format the Creation Kit's newer chunks arrive in, and the one whose absence broke the
    ///     repair. On a client that cannot read it this test does not merely fail -- it fails with the
    ///     user's own exception.
    /// </summary>
    [Fact]
    public void AZstdCompressedChunkIsDecompressedRatherThanReadAsAZip()
    {
        var content = Content(64 * 1024);

        Assert.Equal(content, Process(content, VZstdFrame(content), content.Length));
    }

    /// <summary>The format the game's own depots still use, so the newer one cannot have cost us the old.</summary>
    [Fact]
    public void AZipCompressedChunkStillWorks()
    {
        var content = Content(64 * 1024);

        Assert.Equal(content, Process(content, ZipFrame(content), content.Length));
    }

    /// <summary>
    ///     <see cref="DepotFileAssembler" /> rents its destination from <see cref="ArrayPool{T}" />, which
    ///     answers a request with whatever bucket fits and so nearly always hands back a buffer longer than
    ///     the chunk -- carrying whatever the last borrower left in it. Only the first
    ///     <c>UncompressedLength</c> bytes are ever read back, but the decompressor is the one thing that
    ///     could notice the difference, so it is worth knowing it does not.
    /// </summary>
    [Theory]
    [InlineData(4096)]
    [InlineData(64 * 1024)]
    [InlineData(1024 * 1024)]
    public void AnOversizedDestinationBufferChangesNothing(int size)
    {
        var content = Content(size);
        var destination = ArrayPool<byte>.Shared.Rent(size);

        try
        {
            Assert.True(destination.Length >= size);
            RandomNumberGenerator.Fill(destination);

            Assert.Equal(content, Process(content, VZstdFrame(content), destination));
            Assert.Equal(content, Process(content, ZipFrame(content), destination));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(destination);
        }
    }

    /// <summary>
    ///     Runs a compressed payload through the whole of what a CDN server's answer goes through: the
    ///     encryption Steam applies on the way out, and <see cref="DepotChunk.Process" /> on the way back.
    ///     Returns what landed in the destination, trimmed to what the manifest said to expect.
    /// </summary>
    private static byte[] Process(byte[] content, byte[] compressed, int destinationSize)
    {
        return Process(content, compressed, new byte[destinationSize]);
    }

    private static byte[] Process(byte[] content, byte[] compressed, byte[] destination)
    {
        var encrypted = Encrypt(compressed);

        var chunk = new DepotManifest.ChunkData(RandomNumberGenerator.GetBytes(20), Adler32(content), 0,
            (uint) encrypted.Length, (uint) content.Length);

        var written = DepotChunk.Process(chunk, encrypted, destination, DepotKey);

        Assert.Equal(content.Length, written);
        return destination[..written];
    }

    /// <summary>
    ///     What a content server serves: sixteen bytes of ECB-encrypted IV, then the payload in CBC under
    ///     the depot key.
    /// </summary>
    private static byte[] Encrypt(byte[] payload)
    {
        var iv = RandomNumberGenerator.GetBytes(16);

        using var aes = Aes.Create();
        aes.BlockSize = 128;
        aes.KeySize = 256;
        aes.Key = DepotKey;

        var encryptedIv = aes.EncryptEcb(iv, PaddingMode.None);
        var body = aes.EncryptCbc(payload, iv, PaddingMode.PKCS7);

        var chunk = new byte[encryptedIv.Length + body.Length];
        encryptedIv.CopyTo(chunk, 0);
        body.CopyTo(chunk, encryptedIv.Length);
        return chunk;
    }

    /// <summary>
    ///     Valve's Zstd container: the magic and a CRC32 of the uncompressed bytes, the Zstd frame itself,
    ///     and a fifteen byte footer repeating the CRC32, giving the uncompressed length, and ending in
    ///     <c>zsv</c>. The four bytes before that trailer are not read by anything and are left at zero.
    /// </summary>
    private static byte[] VZstdFrame(byte[] content)
    {
        using var compressor = new ZstdSharp.Compressor();
        var compressed = compressor.Wrap(content);

        var crc = Crc32.HashToUInt32(content);
        var frame = new byte[8 + compressed.Length + 15];

        "VSZa"u8.CopyTo(frame);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), crc);
        compressed.CopyTo(frame.AsSpan(8));

        var footer = frame.AsSpan(8 + compressed.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(footer, crc);
        BinaryPrimitives.WriteInt32LittleEndian(footer[4..], content.Length);
        "zsv"u8.CopyTo(footer[12..]);

        return frame;
    }

    /// <summary>The older container: an ordinary zip holding exactly one entry.</summary>
    private static byte[] ZipFrame(byte[] content)
    {
        using var buffer = new MemoryStream();

        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            using var entry = zip.CreateEntry("z", CompressionLevel.Optimal).Open();
            entry.Write(content);
        }

        return buffer.ToArray();
    }

    /// <summary>
    ///     Steam's per-chunk checksum, which <see cref="DepotChunk.Process" /> checks the decompressed bytes
    ///     against. Adler-32 seeded with zero rather than the one RFC 1950 uses, and written out here so
    ///     these tests still compile -- and so still fail, in the user's own words -- against a version of
    ///     SteamKit that cannot read the chunk in the first place.
    /// </summary>
    private static uint Adler32(ReadOnlySpan<byte> input)
    {
        const uint modulus = 65521;
        uint low = 0, high = 0;

        foreach (var b in input)
        {
            low = (low + b) % modulus;
            high = (high + low) % modulus;
        }

        return (high << 16) | low;
    }

    /// <summary>
    ///     Something that compresses like a real file rather than either extreme: a run of zeros would let
    ///     any format through on size alone, and random bytes would not compress at all.
    /// </summary>
    private static byte[] Content(int size)
    {
        var content = new byte[size];
        var random = new Random(size);

        for (var i = 0; i < size; i++) content[i] = (byte) (random.Next(8) + (i % 32));

        return content;
    }
}
