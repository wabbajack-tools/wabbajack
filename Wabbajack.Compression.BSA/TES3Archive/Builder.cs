using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Compression.BSA.Interfaces;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.BSA.FileStates;

namespace Wabbajack.Compression.BSA.TES3Archive;

public class Builder : IBuilder
{
    private readonly (TES3File state, Stream data)[] _files;
    private readonly TES3State _state;

    public Builder(TES3State state)
    {
        _state = state;
        _files = new (TES3File state, Stream data)[_state.FileCount];
    }

    public async ValueTask AddFile(AFile state, Stream src, CancellationToken token)
    {
        var tesState = (TES3File) state;
        _files[state.Index] = (tesState, src);
    }

    public async ValueTask Build(Stream file, CancellationToken token)
    {
        await using var bw = new BinaryWriter(file, Encoding.Default, true);

        bw.Write(_state.VersionNumber);
        bw.Write(_state.HashOffset);
        bw.Write(_state.FileCount);

        foreach (var (state, _) in _files)
        {
            bw.Write(state.Size);
            bw.Write(state.Offset);
        }

        foreach (var (state, _) in _files) bw.Write(state.NameOffset);

        var orgPos = bw.BaseStream.Position;

        foreach (var (state, _) in _files)
        {
            if (bw.BaseStream.Position != orgPos + state.NameOffset)
                throw new BSAException("Offsets don't match when writing TES3 BSA");
            bw.Write(Encoding.ASCII.GetBytes((string) state.Path));
            bw.Write((byte) 0);
        }

        bw.BaseStream.Position = _state.HashOffset + 12;
        foreach (var (state, _) in _files)
        {
            bw.Write(state.Hash1);
            bw.Write(state.Hash2);
        }

        if (bw.BaseStream.Position != _state.DataOffset)
            throw new InvalidDataException("Data offset doesn't match when writing TES3 BSA");

        foreach (var (state, data) in _files)
        {
            bw.BaseStream.Position = _state.DataOffset + state.Offset;
            await data.CopyToWithStatusAsync(data.Length, bw.BaseStream, token);
            await data.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        return;
    }
}