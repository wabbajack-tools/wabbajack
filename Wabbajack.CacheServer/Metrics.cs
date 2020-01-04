using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nancy;
using Wabbajack.CacheServer.DTOs;
using Wabbajack.Common;

namespace Wabbajack.CacheServer
{
    /// <summary>
    /// Extremely
    /// </summary>
    public class Metrics : NancyModule
    {
        private static SemaphoreSlim _lockObject = new SemaphoreSlim(1);

        public static async Task Log(DateTime timestamp, string action, string subject, string metricsKey = null)
        {
            var msg = new[] {string.Join("\t", new[]{timestamp.ToString(), metricsKey, action, subject})};
            Utils.Log(msg.First());
            var db = Server.Config.Metrics.Connect();
            await db.InsertOneAsync(new Metric {Timestamp = timestamp, Action = action, Subject = subject, MetricsKey = metricsKey});
        }

        public static Task Log(string action, string subject)
        {
            return Log(DateTime.Now, action, subject);
        }

        public Metrics() : base("/")
        {
            Get("/metrics/{Action}/{Value}", HandleMetrics);
            Get("/metrics/chart/", HandleChart);
            Get("/metrics/chart/{Action}/", HandleChart);
            Get("/metrics/chart/{Action}/{Value}/", HandleChart);
            Get("/metrics/ingest/{filename}", HandleBulkIngest);
        }

        private async Task<string> HandleBulkIngest(dynamic arg)
        {
            Log("Bulk Loading " + arg.filename.ToString());

            var lines = File.ReadAllLines(Path.Combine(@"c:\tmp", (string)arg.filename));

            var db = Server.Config.Metrics.Connect();
            
            var data = lines.Select(line => line.Split('\t'))
                .Where(line => line.Length == 3)
                .Select(line => new Metric{ Timestamp = DateTime.Parse(line[0]), Action = line[1], Subject = line[2] })
                .ToList();

            foreach (var metric in data)
                await db.InsertOneAsync(metric);

            return $"Processed {lines.Length} records";
        }

        private async Task<string> HandleMetrics(dynamic arg)
        {
            var date = DateTime.UtcNow;
            await Log(date, arg.Action, arg.Value, Request.Headers[Consts.MetricsKeyHeader].FirstOrDefault());
            return date.ToString();
        }

        private async Task<Response> HandleChart(dynamic arg)
        {
            /*var data = (await GetData()).Select(line => line.Split('\t'))
                .Where(line => line.Length == 3)
                .Select(line => new {date = DateTime.Parse(line[0]), Action = line[1], Value = line[2]});*/

            var q = Server.Config.Metrics.Connect().AsQueryable();

            // Remove guids / Default, which come from testing

            if (arg?.Action != null)
            {
                var action = (string)arg.Action;
                q = q.Where(d => d.Action == action);
            }


            if (arg?.Value != null)
            {
                var value = (string)arg.Value;
                q = q.Where(d => d.Subject.StartsWith(value));
            }

            var data = (await q.Take(Int32.MaxValue).ToListAsync()).AsEnumerable();
            data = data.Where(d => !Guid.TryParse(d.Subject ?? "", out Guid v) && (d.Subject ?? "") != "Default");

            var grouped_and_counted = data.GroupBy(d => d.Timestamp.ToString("yyyy-MM-dd"))
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

        public void Log(string l)
        {
            Utils.Log("Metrics: " + l);
        }
    }
}
