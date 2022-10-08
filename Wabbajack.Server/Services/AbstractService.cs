using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;

namespace Wabbajack.Server.Services;

public interface IStartable
{
    public Task Start();
}

public interface IReportingService
{
    public TimeSpan Delay { get; }
    public DateTime LastStart { get; }
    public DateTime LastEnd { get; }

    public (string, DateTime)[] ActiveWorkStatus { get; }
}

public abstract class AbstractService<TP, TR> : IStartable, IReportingService
{
    protected ILogger<TP> _logger;
    protected QuickSync _quickSync;
    protected AppSettings _settings;

    public AbstractService(ILogger<TP> logger, AppSettings settings, QuickSync quickSync, TimeSpan delay)
    {
        _settings = settings;
        Delay = delay;
        _logger = logger;
        _quickSync = quickSync;
    }

    public TimeSpan Delay { get; }

    public DateTime LastStart { get; private set; }
    public DateTime LastEnd { get; private set; }
    public (string, DateTime)[] ActiveWorkStatus { get; private set; } = { };

    public async Task Start()
    {
        await Setup();
        await _quickSync.Register(this);

        while (true)
        {
            await _quickSync.ResetToken<TP>();
            try
            {
                _logger.LogInformation($"Running: {GetType().Name}");
                ActiveWorkStatus = Array.Empty<(string, DateTime)>();
                LastStart = DateTime.UtcNow;
                await Execute();
                LastEnd = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Running Service Loop");
            }

            var token = await _quickSync.GetToken<TP>();
            try
            {
                await Task.Delay(Delay, token);
            }
            catch (TaskCanceledException)
            {
            }
        }
    }

    public virtual async Task Setup()
    {
    }

    public abstract Task<TR> Execute();

    protected void ReportStarting(string value)
    {
        lock (this)
        {
            ActiveWorkStatus = ActiveWorkStatus.Append((value, DateTime.UtcNow)).ToArray();
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
        var poll = (IStartable) b.ApplicationServices.GetRequiredService(typeof(T));
        poll.Start();
    }
}