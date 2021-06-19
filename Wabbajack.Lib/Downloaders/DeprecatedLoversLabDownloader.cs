using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.Lib.Downloaders
{
    public class DeprecatedLoversLabDownloader : ADeprecatedDownloader<DeprecatedLoversLabDownloader, DeprecatedLoversLabDownloader.State>
    {
        [JsonName("LoversLabDownloader")]
        public class State : ADeprecatedDownloader<DeprecatedLoversLabDownloader, DeprecatedLoversLabDownloader.State>.State
        {
        }
    }
}
