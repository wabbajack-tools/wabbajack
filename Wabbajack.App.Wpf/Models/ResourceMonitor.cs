using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Timers;
using Wabbajack.RateLimiter;

namespace Wabbajack.Models;

public class ResourceMonitor : IDisposable
{
    private readonly IResource[] _resources;
    private readonly Timer _timer;
    
    private readonly Subject<(string Name, long Througput)[]> _updates = new ();
    private (string Name, long Throughput)[] _prev;
    public IObservable<(string Name, long Throughput)[]> Updates => _updates;


    public ResourceMonitor(IEnumerable<IResource> resources)
    {
        _resources = resources.ToArray();
        _timer = new Timer();
        _timer.Interval = 1000;
        _timer.Elapsed += Elapsed;
        _timer.Enabled = true;
        _prev = _resources.Select(x => (x.Name, (long)0)).ToArray();
    }

    private void Elapsed(object? sender, ElapsedEventArgs e)
    {
        var current = _resources.Select(x => (x.Name, x.StatusReport.Transferred)).ToArray();
        var diff = _prev.Zip(current)
            .Select(t => (t.First.Name, (long)((t.Second.Transferred - t.First.Throughput) / (_timer.Interval / 1000.0))))
            .ToArray();
        _prev = current;
        _updates.OnNext(diff);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}