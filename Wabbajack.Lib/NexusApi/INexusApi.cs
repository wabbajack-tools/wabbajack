using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Lib.NexusApi
{
    public interface INexusApi
    {
        public Task<string> GetNexusDownloadLink(NexusDownloader.State archive);
        public Task<NexusApiClient.GetModFilesResponse> GetModFiles(Game game, long modid, bool useCache = true);
        public Task<ModInfo> GetModInfo(Game game, long modId, bool useCache = true);

        public Task<UserStatus> GetUserStatus();
        public Task<bool> IsPremium();
        public bool IsAuthenticated { get; }
    }
}
