using System.IO.Compression;
using System.Text;
using Wabbajack.IO.Async;
using static System.UInt32;

namespace Wabbajack.Compression.Zip;

public class ZipReader : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly AsyncBinaryReader _rdr;
    private readonly bool _leaveOpen;

    private const uint EndOfCentralDirectoryRecordSignature = 0x06054b50;
    private const uint EndOfCentralDirectoryOffsetSignature64 = 0x7064b50;
    private const uint CentralDirectoryFileHeaderSignature = 0x02014b50;
    private const uint EndOfCentralDirectorySignature64 = 0x6064b50;

    public ZipReader(Stream s, bool leaveOpen = false)
    {
        _leaveOpen = leaveOpen;
        _stream = s;
        _rdr = new AsyncBinaryReader(s);
    }

    public async Task<(long sigOffset, uint TotalRecords, long CDOffset)> ReadZip32EODR(long sigOffset)
    {
        while (true)
        {
            _rdr.Position = _rdr.Length - 22 - sigOffset;
            if (await _rdr.ReadUInt32() == EndOfCentralDirectoryRecordSignature)
                break;
            sigOffset++;
        }

        var hdrOffset = _rdr.Position - 4;

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

        return (hdrOffset, totalCentralDirectoryRecords, centralDirectoryOffset);
    }
    
    public async Task<(long sigOffset, uint TotalRecords, long CDOffset)> ReadZip64EODR(long sigOffset)
    {
        while (true)
        {
            _rdr.Position = sigOffset;
            if (await _rdr.ReadUInt32() == EndOfCentralDirectoryOffsetSignature64)
                break;
            sigOffset--;
        }

        var hdrOffset = sigOffset - 4;

        if (await _rdr.ReadUInt32() != 0)
        {
            throw new NotImplementedException("Only single disk archives are supported");
        }

        var ecodOffset = await _rdr.ReadUInt64();

        _rdr.Position = (long)ecodOffset;
        if (await _rdr.ReadUInt32() != EndOfCentralDirectorySignature64)
            throw new Exception("Corrupt Zip64 structure, can't find EOCD");

        var sizeOfECDR = await _rdr.ReadUInt64();

        _rdr.Position += 4; // Skip version info
        
        if (await _rdr.ReadUInt32() != 0)
        {
            throw new NotImplementedException("Only single disk archives are supported");
        }

        if (await _rdr.ReadInt32() != 0)
        {
            throw new NotImplementedException("Only single disk archives are supported");
        }

        var recordsOnDisk = await _rdr.ReadUInt64();
        var totalRecords = await _rdr.ReadUInt64();
        
        if (recordsOnDisk != totalRecords)
        {
            throw new NotImplementedException("Only single disk archives are supported");
        }
        
        var sizeOfCD = await _rdr.ReadUInt64();
        var cdOffset = await _rdr.ReadUInt64();
        
        
        
        return (hdrOffset, (uint)totalRecords, (long)cdOffset);
    }

    public async Task<ExtractedEntry[]> GetFiles()
    {
        var (sigOffset, totalCentralDirectoryRecords, centralDirectoryOffset) = await ReadZip32EODR(0);
        var isZip64 = false;
        if (centralDirectoryOffset == uint.MaxValue)
        {
            isZip64 = true;
            (sigOffset, totalCentralDirectoryRecords, centralDirectoryOffset) = await ReadZip64EODR(sigOffset);
        }
        

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
            long compressedSize = await _rdr.ReadUInt32();
            
            long uncompressedSize = await _rdr.ReadUInt32();
            var fileNameLength = await _rdr.ReadUInt16();
            var extraFieldLength = await _rdr.ReadUInt16();
            var fileCommentLength = await _rdr.ReadUInt16();
            _rdr.Position += 8;
            long fileOffset = await _rdr.ReadUInt32();

            var fileName = await _rdr.ReadFixedSizeString(fileNameLength, Encoding.UTF8);
            

            _rdr.Position += fileCommentLength;

            if (compressedSize == uint.MaxValue || uncompressedSize == uint.MaxValue || fileOffset == uint.MaxValue)
            {
                if (await _rdr.ReadUInt16() != 0x1)
                {
                    throw new Exception("Non Zip64 extra fields not implemented");
                }
                var size = await _rdr.ReadUInt16();
                for (var x = 0; x < size / 8; x++)
                {
                    var value = await _rdr.ReadUInt64();
                    if (compressedSize == uint.MaxValue)
                        compressedSize = (long)value;
                    else if (uncompressedSize == uint.MaxValue)
                        uncompressedSize = (long) value;
                    else if (fileOffset == (long) uint.MaxValue)
                        fileOffset = (long) value;
                    else
                        throw new Exception("Bad zip format");
                    
                }
            }
            else
            {
                _rdr.Position += extraFieldLength;
            }

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
        if (flags != 0)
            throw new Exception("Flags not yet implemented");
        _stream.Position += 18;
        var fnLength = await _rdr.ReadUInt16();
        var efLength = await _rdr.ReadUInt16();
        _stream.Position += fnLength + efLength;

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