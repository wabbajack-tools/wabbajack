using System;
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
                server.Start();
                Console.ReadLine();
            }
        }
    }
}
