using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Exceptions;

namespace Wabbajack.Lib
{
    public class ClientAPI
    {
        public static Common.Http.Client GetClient()
        {
            var client = new Common.Http.Client();
            if (Utils.HaveEncryptedJson(Consts.MetricsKeyHeader)) 
                client.Headers.Add((Consts.MetricsKeyHeader, Utils.FromEncryptedJson<string>(Consts.MetricsKeyHeader)));
            return client;
        }

        public static async Task<Archive> GetModUpgrade(Hash hash)
        {
            using var response = await GetClient()
                .GetAsync($"https://{Consts.WabbajackCacheHostname}/alternative/{hash.ToHex()}");
            return !response.IsSuccessStatusCode ? null : (await response.Content.ReadAsStringAsync()).FromJsonString<Archive>();
        }

        /// <summary>
        /// Given an archive hash, search the Wabbajack server for a matching .ini file
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public static async Task<string> GetModIni(Hash hash)
        {
            var client = new Common.Http.Client();
            try
            {
                return await client.GetStringAsync(
                        $"{Consts.WabbajackBuildServerUri}indexed_files/{hash.ToHex()}/meta.ini");
            }
            catch (HttpException)
            {
                return null;
            }
        }

        public class NexusCacheStats
        {
            public long CachedCount { get; set; }
            public long ForwardCount { get; set; }
            public double CacheRatio { get; set; }
        }

        public static async Task<NexusCacheStats> GetNexusCacheStats()
        {
            return await GetClient()
                .GetJsonAsync<NexusCacheStats>($"{Consts.WabbajackBuildServerUri}nexus_cache/stats");
        }
    }
}
