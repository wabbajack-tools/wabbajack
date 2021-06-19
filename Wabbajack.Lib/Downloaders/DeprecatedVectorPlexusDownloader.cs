using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.Lib.Downloaders
{
    public class DeprecatedVectorPlexusDownloader : ADeprecatedDownloader<DeprecatedVectorPlexusDownloader, DeprecatedVectorPlexusDownloader.State>
    {
        [JsonName("VectorPlexusDownloader")]
        public class State : ADeprecatedDownloader<DeprecatedVectorPlexusDownloader, DeprecatedVectorPlexusDownloader.State>.State
        {
        }
    }
}
