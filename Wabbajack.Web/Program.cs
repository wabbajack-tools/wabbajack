using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;

namespace Wabbajack.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var logFactory = NLogBuilder.ConfigureNLog("nlog.config");
            var logger = logFactory.GetCurrentClassLogger();
            
            logger.Info("Creating Host");
            var host = CreateHostBuilder(args).Build();
            
            logger.Info("Starting Application");
            try
            {
                host.Run();
            }
            catch (Exception e)
            {
                logger.Error(e, "Application stopped because of an exception");
                throw;
            }
            finally
            {
                NLog.LogManager.Shutdown();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Trace);
                })
                .UseNLog();
    }
}
