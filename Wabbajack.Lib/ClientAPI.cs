using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib
{
    public class ClientAPI
    {
        public static Common.Http.Client GetClient()
        {
            var client = new Common.Http.Client();
            client.Headers.Add((Consts.MetricsKeyHeader, Utils.FromEncryptedJson<string>(Consts.MetricsKeyHeader)));
            return client;
        }

        public static async Task<Archive> GetModUpgrade(string hash)
        {
            using var response = await GetClient()
                .GetAsync($"https://{Consts.WabbajackCacheHostname}/alternative/{hash.FromBase64().ToHex()}");
            return !response.IsSuccessStatusCode ? null : (await response.Content.ReadAsStringAsync()).FromJSONString<Archive>();
        }
    }
}
