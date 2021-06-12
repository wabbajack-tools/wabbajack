using System.Threading.Tasks;
using Wabbajack.Common.Serialization.Json;

namespace Compression.BSA
{
    [JsonName("BSAState")]
    public class BSAStateObject : ArchiveStateObject
    {
        public string Magic { get; set; } = string.Empty;
        public uint Version { get; set; }
        public uint ArchiveFlags { get; set; }
        public uint FileFlags { get; set; }

        public BSAStateObject()
        {
        }

        public BSAStateObject(BSAReader bsaReader)
        {
            Magic = bsaReader._magic;
            Version = (uint)bsaReader.HeaderType;
            ArchiveFlags = (uint)bsaReader.ArchiveFlags;
            FileFlags = (uint)bsaReader.FileFlags;
        }

        public override async Task<IBSABuilder> MakeBuilder(long size)
        {
            return await BSABuilder.Create(this, size).ConfigureAwait(false);
        }
    }
}
