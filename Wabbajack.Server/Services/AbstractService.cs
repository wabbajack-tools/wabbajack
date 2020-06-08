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
        protected QuickSync _quickSync;

        public AbstractService(ILogger<TP> logger, AppSettings settings, QuickSync quickSync, TimeSpan delay)
        {
            _settings = settings;
            _delay = delay;
            _logger = logger;
            _quickSync = quickSync;
        }

        public void Start()
        {
            if (_settings.RunBackEndJobs)
            {
                Task.Run(async () =>
                {
                    while (true)
                    {
                        await _quickSync.ResetToken<TP>();
                        try
                        {
                            await Execute();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Running Service Loop");
                        }

                        var token = await _quickSync.GetToken<TP>();
                        try
                        {
                            await Task.Delay(_delay, token);
                        }
                        catch (TaskCanceledException)
                        {
                            
                        }
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
