using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public class Metrics
    {
        static Metrics()
        {
            if (!Utils.HaveEncryptedJson(Consts.MetricsKeyHeader))
            {
                Utils.ToEcryptedJson(Utils.MakeRandomKey(), Consts.MetricsKeyHeader);
            }
        }
        /// <summary>
        /// This is all we track for metrics, action, and value. The action will be like
        /// "downloaded", the value "Joe's list".
        /// </summary>
        /// <param name="action"></param>
        /// <param name="value"></param>
        public static async Task Send(string action, string value)
        {
            var client = new HttpClient();
            try
            {
                client.DefaultRequestHeaders.Add(Consts.MetricsKeyHeader,
                    Utils.FromEncryptedJson<string>(Consts.MetricsKeyHeader));
                await client.GetAsync($"http://build.wabbajack.org/metrics/{action}/{value}");
            }
            catch (Exception ex)
            {
                
            }
        }
    }
}
