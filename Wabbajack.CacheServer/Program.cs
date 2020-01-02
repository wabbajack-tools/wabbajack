using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy.Hosting.Self;
using Wabbajack.Common;

namespace Wabbajack.CacheServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Utils.LogMessages.Subscribe(Console.WriteLine);
            using (var server = new Server("http://localhost:8080"))
            {
                Consts.WabbajackCacheHostname = "localhost";
                Consts.WabbajackCachePort = 8080;
                server.Start();

                ListValidationService.Start();
                var tsk = JobQueueEndpoints.StartJobQueue();
                
                Console.ReadLine();
            }
        }
    }
}
