using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;

namespace Wabbajack.Server.Services
{
    public interface IStartable
    {
        public void Start();
    }
    
    public abstract class AbstractService<TP, TR> : IStartable
    {
        protected AppSettings _settings;
        private TimeSpan _delay;
        protected ILogger<TP> _logger;

        public AbstractService(ILogger<TP> logger, AppSettings settings, TimeSpan delay)
        {
            _settings = settings;
            _delay = delay;
            _logger = logger;
        }

        public void Start()
        {
            if (_settings.RunBackEndJobs)
            {
                Task.Run(async () =>
                {
                    while (true)
                    {

                        try
                        {
                            await Execute();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Running Service Loop");
                        }

                        await Task.Delay(_delay);
                    }

                });
            }
        }

        public abstract Task<TR> Execute();
    }
    
    public static class AbstractServiceExtensions 
    {
        public static void UseService<T>(this IApplicationBuilder b)
        {
            var poll = (IStartable)b.ApplicationServices.GetService(typeof(T));
            poll.Start();
        }
    
    }
}
