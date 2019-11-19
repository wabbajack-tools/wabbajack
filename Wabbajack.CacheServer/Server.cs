using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Nancy.Hosting.Self;

namespace Wabbajack.CacheServer
{
    public class Server : IDisposable
    {
        private NancyHost _server;
        private HostConfiguration _config;

        public Server(string address)
        {
            Address = address;
            _config = new HostConfiguration();
            //_config.UrlReservations.CreateAutomatically = true;
            _config.RewriteLocalhost = true;
            _server = new NancyHost(_config, new Uri(address));
        }

        public string Address { get; }

        public void Start()
        {
            _server.Start();
        }

        public void Dispose()
        {
            _server?.Dispose();
        }
    }
}
