using System.IO.Compression;
using System.Text;
using Wabbajack.IO.Async;

namespace Wabbajack.Compression.Zip;

public class ZipReader : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly AsyncBinaryReader _rdr;
    private readonly bool _leaveOpen;

    private const uint EndOfCentralDirectoryRecordSignature = 0x06054b50;
    private const uint CentralDirectoryFileHeaderSignature = 0x02014b50;

    public ZipReader(Stream s, bool leaveOpen = false)
    {
        _leaveOpen = leaveOpen;
        _stream = s;
        _rdr = new AsyncBinaryReader(s);
    }

    public async Task<ExtractedEntry[]> GetFiles()
    {
        var sigOffset = 0;
        while (true)
        {
            _rdr.Position = _rdr.Length - 22 - sigOffset;
            if (await _rdr.ReadUInt32() == EndOfCentralDirectoryRecordSignature)
                break;
            sigOffset++;
        }

        if (await _rdr.ReadUInt16() != 0)
        {
            throw new NotImplementedException("Only single disk archives are supported");
        }

        if (await _rdr.ReadInt16() != 0)
        {
            throw new NotImplementedException("Only single disk archives are supported");
        }

        _rdr.Position += 2;

        var totalCentralDirectoryRecords = await _rdr.ReadUInt16();
        var sizeOfCentralDirectory = await _rdr.ReadUInt32();
        var centralDirectoryOffset = await _rdr.ReadUInt32();


        _rdr.Position = centralDirectoryOffset;


        var entries = new ExtractedEntry[totalCentralDirectoryRecords];
        for (var i = 0; i < totalCentralDirectoryRecords; i += 1)
        {
            if (await _rdr.ReadUInt32() != CentralDirectoryFileHeaderSignature)
                throw new Exception("Data corruption, can't find central directory");
            
            _rdr.Position += 6;
            var compressionMethod = await _rdr.ReadInt16();
            _rdr.Position += 4;
            var crc = await _rdr.ReadUInt32();
            var compressedSize = await _rdr.ReadUInt32();
            
            var uncompressedSize = await _rdr.ReadUInt32();
            var fileNameLength = await _rdr.ReadUInt16();
            var extraFieldLength = await _rdr.ReadUInt16();
            var fileCommentLength = await _rdr.ReadUInt16();
            _rdr.Position += 8;
            var fileOffset = await _rdr.ReadUInt32();
            var fileName = await _rdr.ReadFixedSizeString(fileNameLength, Encoding.UTF8);

            _rdr.Position += extraFieldLength + fileCommentLength;

            entries[i] = new ExtractedEntry
            {
                FileOffset = fileOffset,
                FileName = fileName,
                CompressedSize = compressedSize,
                UncompressedSize = uncompressedSize,
                CompressionMethod = compressionMethod,
            };
        }


        return entries;

    }

    public async ValueTask Extract(ExtractedEntry entry, Stream stream, CancellationToken token)
    {
        _stream.Position = entry.FileOffset;
        _stream.Position += 6;
        var flags = await _rdr.ReadUInt16();
        _stream.Position += 18;
        var fnLength = await _rdr.ReadUInt16();
        var efLength = await _rdr.ReadUInt16();
        _stream.Position += fnLength + efLength;

        if (flags != 0)
            throw new NotImplementedException("Don't know how to handle flags yet");
        
        
        switch (entry.CompressionMethod)
        {
            case 0:
                await _stream.CopyToLimitAsync(stream, (int) entry.UncompressedSize, token);
                break;
            case 1:
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
            case 7:
            case 8:
                var ds = new DeflateStream(_rdr.BaseStream, CompressionMode.Decompress, true);
                await ds.CopyToLimitAsync(stream, (int)entry.UncompressedSize, token);
                break;
            default:
                throw new NotImplementedException($"Have not implemented compression format {entry.CompressionMethod}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_leaveOpen)
            await _stream.DisposeAsync();
    }
}