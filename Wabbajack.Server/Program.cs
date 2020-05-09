using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Wabbajack.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            bool testMode = args.Contains("TESTMODE");
            CreateHostBuilder(args, testMode).Build().Run();
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args, bool testMode) => 
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                        .UseKestrel(options =>
                        {
                            options.Listen(IPAddress.Any, testMode ? 8080 : 80);
                            if (!testMode)
                            {
                                options.Listen(IPAddress.Any, 443, listenOptions =>
                                {
                                    using (var store = new X509Store(StoreName.My))
                                    {
                                        store.Open(OpenFlags.ReadOnly);
                                        var cert = store.Certificates.Find(X509FindType.FindBySubjectName,
                                            "build.wabbajack.org", true)[0];
                                        listenOptions.UseHttps(cert);

                                    }
                                });
                            }
                            options.Limits.MaxRequestBodySize = null;
                        });
                });
    }
}
