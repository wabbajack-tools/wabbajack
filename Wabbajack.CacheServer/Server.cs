using System;
using Microsoft.Owin.Hosting;

namespace Wabbajack.CacheServer
{
    public class Server : IDisposable
    {
        private IDisposable _host;

        public Server(string address)
        {
            Address = address;
        }

        public string Address { get; }

        public void Start()
        {
            var options = new StartOptions(Address);
            _host = WebApp.Start<Startup>(options);
        }

        public void Dispose()
        {
            _host?.Dispose();
        }
    }
}
