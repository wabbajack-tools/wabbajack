using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Compression.BSA
{
    public class TES3Builder : IBSABuilder
    {
        private TES3ArchiveState _state;
        private (TES3FileState state, Stream data)[] _files;

        public TES3Builder(TES3ArchiveState state)
        {
            _state = state;
            _files = new (TES3FileState state, Stream data)[_state.FileCount];
        }

        public async Task AddFile(FileStateObject state, Stream src)
        {
            var cstate = (TES3FileState)state;
            _files[state.Index] = (cstate, src);
        }

        public async Task Build(AbsolutePath filename)
        {
            await using var fs = await filename.Create();
            await using var bw = new BinaryWriter(fs);
            
            bw.Write(_state.VersionNumber);
            bw.Write(_state.HashOffset);
            bw.Write(_state.FileCount);

            foreach (var (state, _) in _files)
            {
                bw.Write(state.Size);
                bw.Write(state.Offset);
            }

            foreach (var (state, _) in _files)
            {
                bw.Write(state.NameOffset);
            }

            var orgPos = bw.BaseStream.Position;
            
            foreach (var (state, _) in _files)
            {
                if (bw.BaseStream.Position != orgPos + state.NameOffset)
                    throw new InvalidDataException("Offsets don't match when writing TES3 BSA");
                bw.Write(Encoding.ASCII.GetBytes((string)state.Path));
                bw.Write((byte)0);
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
                await data.CopyToAsync(bw.BaseStream);
                await data.DisposeAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
        }
    }
}
