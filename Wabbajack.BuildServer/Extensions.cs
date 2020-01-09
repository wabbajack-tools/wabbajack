using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.Common;

namespace Wabbajack.BuildServer
{
    public static class Extensions
    {
        public static async Task<T> FindOneAsync<T>(this IMongoCollection<T> coll, Expression<Func<T, bool>> expr)
        {
            return (await coll.AsQueryable().Where(expr).Take(1).ToListAsync()).FirstOrDefault();
        }

        public static void UseJobManager(this IApplicationBuilder b)
        {
            var manager = (JobManager)b.ApplicationServices.GetService(typeof(JobManager));
            var tsk = manager.JobScheduler();

            manager.StartJobRunners();
        }
    }
}
