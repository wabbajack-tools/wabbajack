using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Compression.BSA.Interfaces;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compression.BSA.BA2Archive;

public class Builder : IBuilder
{
    private List<IFileBuilder> _entries = new();
    private DiskSlabAllocator _slab;
    private BA2State _state;

    public async ValueTask AddFile(AFile state, Stream src, CancellationToken token)
    {
        try
        {
            switch (_state.Type)
            {
                case BA2EntryType.GNRL:
                    var result = await FileEntryBuilder.Create((BA2File)state, src, _slab, token);
                    lock (_entries)
                    {
                        _entries.Add(result);
                    }

                    break;
                case BA2EntryType.DX10:
                    var resultdx10 = await DX10FileEntryBuilder.Create((BA2DX10File)state, src, _slab, _state.Compression == 3, token);
                    lock (_entries)
                    {
                        _entries.Add(resultdx10);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Error adding file {state.Path} to archive: {ex.Message}", ex);
        }
    }

    public async ValueTask Build(Stream fs, CancellationToken token)
    {
        SortEntries();
        await using var bw = new BinaryWriter(fs, Encoding.Default, true);

        bw.Write(Encoding.ASCII.GetBytes(_state.HeaderMagic));
        bw.Write(_state.Version);
        bw.Write(Encoding.ASCII.GetBytes(Enum.GetName(_state.Type)!));
        bw.Write((uint) _entries.Count);
        var tableOffsetLoc = bw.BaseStream.Position;
        bw.Write((ulong) 0);
        if(_state.Version == 2 || _state.Version == 3)
        {
            bw.Write(_state.Unknown1);
            bw.Write(_state.Unknown2);
            if (_state.Version == 3)
                bw.Write(_state.Compression);
        }

        foreach (var entry in _entries) entry.WriteHeader(bw, token);

        foreach (var entry in _entries) await entry.WriteData(bw, token);

        if (!_state.HasNameTable) return;

        var pos = bw.BaseStream.Position;
        bw.BaseStream.Seek(tableOffsetLoc, SeekOrigin.Begin);
        bw.Write((ulong) pos);
        bw.BaseStream.Seek(pos, SeekOrigin.Begin);

        foreach (var entry in _entries)
        {
            var bytes = Encoding.UTF8.GetBytes(entry.FullName);
            bw.Write((ushort) bytes.Length);
            await bw.BaseStream.WriteAsync(bytes, token);
        }
    }

    public static Builder Create(BA2State state, TemporaryFileManager manager)
    {
        var self = new Builder {_state = state, _slab = new DiskSlabAllocator(manager)};
        return self;
    }

    public async ValueTask DisposeAsync()
    {
        await _slab.DisposeAsync();
    }

    private void SortEntries()
    {
        _entries = _entries.OrderBy(e => e.Index).ToList();
    }
}