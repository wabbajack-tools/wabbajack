using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Wabbajack.Common;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.BuildServer
{
    public static class Extensions
    {
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
        
        private static readonly ConcurrentDictionary<Hash, AbsolutePath> PathForArchiveHash = new ConcurrentDictionary<Hash, AbsolutePath>();
        public static AbsolutePath PathForArchive(this AppSettings settings, Hash hash)
        {
            if (PathForArchiveHash.TryGetValue(hash, out AbsolutePath result))
                return result;
            
            var hexHash = hash.ToHex();

            var ends = "_" + hexHash + "_";
            var file = settings.ArchivePath.EnumerateFiles()
                .FirstOrDefault(f => ((string)f.FileNameWithoutExtension).EndsWith(ends)); 

            if (file != default) 
                PathForArchiveHash.TryAdd(hash, file);
            return file;
        }
    }
}
