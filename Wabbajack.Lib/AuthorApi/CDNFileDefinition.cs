using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.Lib.AuthorApi
{
    [JsonName("CDNFileDefinition")]
    public class CDNFileDefinition
    {
        public string? Author { get; set; }
        public RelativePath OriginalFileName { get; set; }
        public long Size { get; set; }
        public Hash Hash { get; set; }
        public CDNFilePartDefinition[] Parts { get; set; } = { };
        public string? ServerAssignedUniqueId { get; set; }
        public string MungedName => $"{OriginalFileName}_{ServerAssignedUniqueId!}";
    }

    [JsonName("CDNFilePartDefinition")]
    public class CDNFilePartDefinition
    {
        public long Size { get; set; }
        public long Offset { get; set; }
        public Hash Hash { get; set; }
        public long Index { get; set; }
    }
}
