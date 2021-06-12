using System;
using System.Net;
using System.Net.Security;
using Wabbajack.Common;
using SysHttp = System.Net.Http;

namespace Wabbajack.Lib.Http
{
    public static class ClientFactory
    {
        private static SysHttp.SocketsHttpHandler _socketsHandler { get; }
        public static SysHttp.HttpClient Client { get; }
        internal static CookieContainer Cookies { get; }

        static ClientFactory()
        {
            Cookies = new CookieContainer();
            _socketsHandler = new SysHttp.SocketsHttpHandler
            {
                CookieContainer = Cookies,
                MaxConnectionsPerServer = 20,
                PooledConnectionLifetime = TimeSpan.FromMilliseconds(100),
                PooledConnectionIdleTimeout = TimeSpan.FromMilliseconds(100),
                AutomaticDecompression = DecompressionMethods.All,

            };
            Utils.Log($"Configuring with SSL {_socketsHandler.SslOptions.EnabledSslProtocols}");

            ServicePointManager.ServerCertificateValidationCallback +=
                (sender, certificate, chain, errors) =>
                {
                    if (Consts.UseNetworkWorkaroundMode)
                        return true;
                    return errors == SslPolicyErrors.None;
                };
            Client = new SysHttp.HttpClient(_socketsHandler);
            Client.DefaultRequestHeaders.Add("User-Agent", Consts.UserAgent);
        }
    }
}
