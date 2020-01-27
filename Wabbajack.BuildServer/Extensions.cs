using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using File = Alphaleonis.Win32.Filesystem.File;

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
        
        public static async Task CopyFileAsync(string sourcePath, string destinationPath)
        {
            using (Stream source = File.OpenRead(sourcePath))
            {
                using(Stream destination = File.Create(destinationPath))
                {
                    await source.CopyToAsync(destination);
                }
            }
        }
       
        public static AuthenticationBuilder AddApiKeySupport(this AuthenticationBuilder authenticationBuilder, Action<ApiKeyAuthenticationOptions> options)
        {
            return authenticationBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.DefaultScheme, options);
        }
    }
}
