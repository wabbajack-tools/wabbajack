using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Nancy;
using ReactiveUI;
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
            Get("/metrics/chart/", HandleChart);
            Get("/metrics/chart/{Action}/", HandleChart);
            Get("/metrics/chart/{Action}/{Value}/", HandleChart);
        }

        private async Task<string> HandleMetrics(dynamic arg)
        {
            var date = DateTime.UtcNow;
            await Log(date, arg.Action, arg.Value);
            return date.ToString();
        }

        private static async Task<string[]> GetData()
        {
            await _lockObject.WaitAsync();
            try
            {
                return File.ReadAllLines("stats.tsv");
            }
            finally
            {
                _lockObject.Release();
            }
        }

        private async Task<Response> HandleChart(dynamic arg)
        {
            var data = (await GetData()).Select(line => line.Split('\t'))
                .Where(line => line.Length == 3)
                .Select(line => new {date = DateTime.Parse(line[0]), Action = line[1], Value = line[2]});

            // Remove guids / Default, which come from testing
            data = data.Where(d => !Guid.TryParse(d.Value ?? "", out _) && (d.Value ?? "") != "Default");

            if (arg?.Action != null)
                data = data.Where(d => d.Action == arg.Action);


            if (arg?.Value != null)
                data = data.Where(d => d.Value.StartsWith(arg.Value));

            var grouped_and_counted = data.GroupBy(d => d.date.ToString("yyyy-MM-dd"))
                .OrderBy(d => d.Key)
                .Select(d => new {Day = d.Key, Count = d.Count()})
                .ToList();

            var sb = new StringBuilder();
            sb.Append("<html><head><script src=\"https://cdn.jsdelivr.net/npm/chart.js@2.8.0\"></script></head>");
            sb.Append("<body><canvas id=\"myChart\"></canvas>");
            sb.Append("<script language='javascript'>");
            var script = @"var ctx = document.getElementById('myChart').getContext('2d');
                    var chart = new Chart(ctx, {
                                    // The type of chart we want to create
                                        type: 'line',

                        // The data for our dataset
                        data: {
                                    labels: [{{LABELS}}],
                                    datasets: [{
                                        label: '{{DATASET}}',
                                        backgroundColor: 'rgb(255, 99, 132)',
                                        borderColor: 'rgb(255, 99, 132)',
                                        data: [{{DATA}}]
                                    }]
                                },

            // Configuration options go here
            options: {}
            });";
            sb.Append(script.Replace("{{LABELS}}", string.Join(",", grouped_and_counted.Select(e => "'"+e.Day+"'")))
                .Replace("{{DATA}}", string.Join(",", grouped_and_counted.Select(e => e.Count.ToString())))
                .Replace("{{DATASET}}", (arg.Action ?? "*") + " - " + (arg.Value ?? "*")));

            sb.Append("</script>");
            sb.Append("</body></html>");
            var response = (Response)sb.ToString();
            response.ContentType = "text/html";
            return response;
        }
    }
}
