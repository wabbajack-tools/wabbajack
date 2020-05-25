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
        public const string Downloading = "downloading";
        public const string BeginInstall = "begin_install";
        public const string FinishInstall = "finish_install";

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
                    await Utils.FromEncryptedJson<string>(Consts.MetricsKeyHeader));
                await client.GetAsync($"{Consts.WabbajackBuildServerUri}metrics/{action}/{value}");
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
