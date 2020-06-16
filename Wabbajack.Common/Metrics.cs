using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Wabbajack.Common.Exceptions;

namespace Wabbajack.Common
{
    public class Metrics
    {
        public const string Downloading = "downloading";
        public const string BeginInstall = "begin_install";
        public const string FinishInstall = "finish_install";
        private static AsyncLock _creationLock = new AsyncLock();

        public static async ValueTask<string> GetMetricsKey()
        {
            using var _ = await _creationLock.WaitAsync();
            if (!Utils.HaveEncryptedJson(Consts.MetricsKeyHeader))
            {
                if (!Utils.HaveRegKeyMetricsKey())
                {
                    // When there's no file or regkey
                    var key = Utils.MakeRandomKey();
                    await key.ToEcryptedJson(Consts.MetricsKeyHeader);
                    using (RegistryKey regKey = Registry.CurrentUser.CreateSubKey(@"Software\Wabbajack", RegistryKeyPermissionCheck.Default)!)
                    {
                        regKey.SetValue("x-metrics-key", key);
                    }
                    return key;
                }
                else
                {
                    // If there is no file but a registry key, create the file and transfer the data from the registry key
                    using (RegistryKey regKey = Registry.CurrentUser.OpenSubKey(@"Software\Wabbajack", RegistryKeyPermissionCheck.Default)!)
                    {
                        string key = (string)regKey.GetValue(Consts.MetricsKeyHeader)!;
                        await key.ToEcryptedJson(Consts.MetricsKeyHeader);
                        return key;
                    }
                }
            }
            else
            {
                if (Utils.HaveRegKeyMetricsKey())
                {
                    // When there's a file and a regkey
                    using (RegistryKey regKey = Registry.CurrentUser.OpenSubKey(@"Software\Wabbajack", RegistryKeyPermissionCheck.Default)!)
                    {
                        return (string)regKey.GetValue(Consts.MetricsKeyHeader)!;
                    }
                }
                else
                {
                    // If there's a regkey and a file, return regkey
                    using (RegistryKey regKey = Registry.CurrentUser.CreateSubKey(@"Software\Wabbajack", RegistryKeyPermissionCheck.Default)!)
                    {
                        string key = await Utils.FromEncryptedJson<string>(Consts.MetricsKeyHeader)!;
                        regKey.SetValue("x-metrics-key", key);
                        return key;
                    }
                }
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
            var key = await GetMetricsKey();
            Utils.Log($"File hash check (-42) {key}");
            var client = new Http.Client();
            client.Headers.Add((Consts.MetricsKeyHeader, key));
            await client.GetAsync($"{Consts.WabbajackBuildServerUri}metrics/{action}/{value}");
        }
    }
}
