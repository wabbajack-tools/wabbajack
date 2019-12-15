using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Nancy;
using Wabbajack.Common;

namespace Wabbajack.CacheServer
{
    /// <summary>
    /// Extremely
    /// </summary>
    public class Metrics : NancyModule
    {
        private static SemaphoreSlim _lockObject = new SemaphoreSlim(1);

        public static async Task Log(params object[] args)
        {
            var msg = new[] {string.Join("\t", args.Select(a => a.ToString()))};
            Utils.Log(msg.First());
            await _lockObject.WaitAsync();
            try
            {
                File.AppendAllLines("stats.tsv", msg);
            }
            finally
            {
                _lockObject.Release();
            }
        }

        public Metrics() : base("/")
        {
            Get("/metrics/{Action}/{Value}", HandleMetrics);
        }

        private async Task<string> HandleMetrics(dynamic arg)
        {
            var date = DateTime.UtcNow;
            await Log(date, arg.Action, arg.Value);
            return date.ToString();
        }
    }
}
