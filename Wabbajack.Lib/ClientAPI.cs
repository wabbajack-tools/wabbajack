using System.Threading.Tasks;
using Wabbajack.Common;

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
            return !response.IsSuccessStatusCode ? null : (await response.Content.ReadAsStringAsync()).FromJSONString<Archive>();
        }
    }
}
