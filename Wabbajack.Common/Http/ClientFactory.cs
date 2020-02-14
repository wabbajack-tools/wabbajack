using System;
using System.Net;
using SysHttp = System.Net.Http;
namespace Wabbajack.Common.Http
{
    public static class ClientFactory
    {
        private static SysHttp.SocketsHttpHandler _socketsHandler { get; }
        internal static SysHttp.HttpClient Client { get; }
        internal static CookieContainer Cookies { get; }

        static ClientFactory()
        {
            Cookies = new CookieContainer();
            _socketsHandler = new SysHttp.SocketsHttpHandler
            {
                CookieContainer = Cookies,
                MaxConnectionsPerServer = 8,
                PooledConnectionLifetime = TimeSpan.FromSeconds(2),
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(2)
            };
            Client = new SysHttp.HttpClient(_socketsHandler);
        }
    }
}
