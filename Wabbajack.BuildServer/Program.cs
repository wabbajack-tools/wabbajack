using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wabbajack.BuildServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                        .UseKestrel(options =>
                        {
                            options.Listen(IPAddress.Any, 80);
                            options.Listen(IPAddress.Any, 443, listenOptions =>
                            {
                                using (var store = new X509Store(StoreName.My))
                                {
                                    store.Open(OpenFlags.ReadOnly);
                                    var cert = store.Certificates.Find(X509FindType.FindBySubjectName, "build.wabbajack.org", true)[0];
                                    listenOptions.UseHttps(cert);
                                    
                                }
                            });
                            options.Limits.MaxRequestBodySize = null;
                        });
                });
    }
}
