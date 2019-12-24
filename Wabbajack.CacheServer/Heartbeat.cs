using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy;

namespace Wabbajack.CacheServer
{
    public class Heartbeat : NancyModule
    {
        private static DateTime startTime = DateTime.Now;

        public Heartbeat() : base("/")
        {
            Get("/heartbeat", HandleHeartbeat);
        }

        private object HandleHeartbeat(object arg)
        {
            return $"Service is live for: {DateTime.Now - startTime}";
        }
    }
}
