using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Wabbajack.Common;
using Wabbajack.DTOs.BSA.FileStates;

namespace Wabbajack.Compression.BSA.BA2Archive;

public class FileEntryBuilder : IFileBuilder
{
    private Stream _dataSrc;
    private long _offsetOffset;
    private int _rawSize;
    private int _size;
    private BA2File _state;

    public uint FileHash => _state.NameHash;
    public uint DirHash => _state.DirHash;
    public string FullName => (string) _state.Path;
    public int Index => _state.Index;

    public void WriteHeader(BinaryWriter wtr, CancellationToken token)
    {
        wtr.Write(_state.NameHash);
        wtr.Write(Encoding.UTF8.GetBytes(_state.Extension));
        wtr.Write(_state.DirHash);
        wtr.Write(_state.Flags);
        _offsetOffset = wtr.BaseStream.Position;
        wtr.Write((ulong) 0);
        wtr.Write(_size);
        wtr.Write(_rawSize);
        wtr.Write(_state.Align);
    }

    public async ValueTask WriteData(BinaryWriter wtr, CancellationToken token)
    {
        var pos = wtr.BaseStream.Position;
        wtr.BaseStream.Position = _offsetOffset;
        wtr.Write((ulong) pos);
        wtr.BaseStream.Position = pos;
        _dataSrc.Position = 0;
        await _dataSrc.CopyToLimitAsync(wtr.BaseStream, (int) _dataSrc.Length, token);
        await _dataSrc.DisposeAsync();
    }

    public static async ValueTask<FileEntryBuilder> Create(BA2File state, Stream src, DiskSlabAllocator slab,
        CancellationToken token)
    {
        var builder = new FileEntryBuilder
        {
            _state = state,
            _rawSize = (int) src.Length,
            _dataSrc = src
        };

        if (!state.Compressed)
            return builder;

        await using var ms = new MemoryStream();
        await using (var ds = new DeflaterOutputStream(ms))
        {
            ds.IsStreamOwner = false;
            await builder._dataSrc.CopyToAsync(ds, token);
        }

        await builder._dataSrc.DisposeAsync();
        builder._dataSrc = slab.Allocate(ms.Length);
        ms.Position = 0;
        await ms.CopyToAsync(builder._dataSrc, token);
        builder._dataSrc.Position = 0;
        builder._size = (int) ms.Length;
        return builder;
    }
}