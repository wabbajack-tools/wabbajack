using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;

namespace Wabbajack.Server.Services
{
    public interface IStartable
    {
        public void Start();
    }

    public interface IReportingService
    {
        public TimeSpan Delay { get; }
        public DateTime LastStart { get; }
        public DateTime LastEnd { get; }
        
        public (String, DateTime)[] ActiveWorkStatus { get; }

        
    }
    
    public abstract class AbstractService<TP, TR> : IStartable, IReportingService
    {
        protected AppSettings _settings;
        private TimeSpan _delay;
        protected ILogger<TP> _logger;
        protected QuickSync _quickSync;

        public TimeSpan Delay => _delay;
        public DateTime LastStart { get; private set; }
        public DateTime LastEnd { get; private set; }
        public (String, DateTime)[] ActiveWorkStatus { get; private set; }= { };

        public AbstractService(ILogger<TP> logger, AppSettings settings, QuickSync quickSync, TimeSpan delay)
        {
            _settings = settings;
            _delay = delay;
            _logger = logger;
            _quickSync = quickSync;

        }

        public virtual async Task Setup()
        {
            
        }

        public void Start()
        {

            if (_settings.RunBackEndJobs)
            {
                Task.Run(async () =>
                {
                    await Setup();
                    await _quickSync.Register(this);
                    
                    while (true)
                    {
                        await _quickSync.ResetToken<TP>();
                        try
                        {
                            _logger.LogInformation($"Running: {GetType().Name}");
                            ActiveWorkStatus = Array.Empty<(String, DateTime)>();
                            LastStart = DateTime.UtcNow;
                            await Execute();
                            LastEnd = DateTime.UtcNow;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Running Service Loop");
                            Utils.Error($"Error in service {this.GetType()} : {ex}");
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
        
        protected void ReportStarting(string value)
        {
            lock (this)
            {
                ActiveWorkStatus = ActiveWorkStatus.Cons((value, DateTime.UtcNow)).ToArray();
            }
        }

        protected void ReportEnding(string value)
        {
            lock (this)
            {
                ActiveWorkStatus = ActiveWorkStatus.Where(x => x.Item1 != value).ToArray();
            }
        }
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
