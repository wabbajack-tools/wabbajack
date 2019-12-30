using System;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nancy;
using Nettle;
using Wabbajack.CacheServer.DTOs.JobQueue;

namespace Wabbajack.CacheServer
{
    public class JobQueueEndpoints : NancyModule
    {
        public JobQueueEndpoints() : base ("/jobs")
        {
            Get("/", HandleListJobs);
        }

        private readonly Func<object, string> HandleListJobsTemplate = NettleEngine.GetCompiler().Compile(@"
                <html><head/><body>

                <h2>Jobs - {{$.jobs.Count}} Pending</h2>
                <h3>{{$.time}}</h3>
                <ol>
                {{each $.jobs}}
                    <li>{{$.Description}}</li>
                {{/each}}
                </ol>

                <script>
                setTimeout(function() { location.reload();}, 10000);
                </script>

                </body></html>");

        private async Task<Response> HandleListJobs(object arg)
        {
            var jobs = await Server.Config.JobQueue.Connect()
                .AsQueryable<Job>()
                .Where(j => j.Ended == null)
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.Created)
                .ToListAsync();

            var response = (Response)HandleListJobsTemplate(new {jobs, time = DateTime.Now});
            response.ContentType = "text/html";
            return response;
        }
    }
}
