using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Wabbajack.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var testMode = args.Contains("TESTMODE");
        CreateHostBuilder(args, testMode).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args, bool testMode)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>()
                    .UseKestrel(options =>
                    {
                        options.AllowSynchronousIO = true;
                        options.Listen(IPAddress.Any, 5000);
                    });
            });
    }
}