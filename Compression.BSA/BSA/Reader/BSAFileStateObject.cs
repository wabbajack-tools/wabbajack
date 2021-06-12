using Wabbajack.Common.Serialization.Json;

namespace Compression.BSA
{
    [JsonName("BSAFileState")]
    public class BSAFileStateObject : FileStateObject
    {
        public bool FlipCompression { get; set; }

        public BSAFileStateObject() { }

        public BSAFileStateObject(FileRecord fileRecord)
        {
            FlipCompression = fileRecord.FlipCompression;
            Path = fileRecord.Path;
            Index = fileRecord._index;
        }
    }
}
