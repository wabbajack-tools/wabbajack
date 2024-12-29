using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using DynamicData;
using DynamicData.Kernel;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.RateLimiter;

namespace Wabbajack.Models;

public class ResourceMonitor : IDisposable
{
    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(1000);
    
    private readonly IResource[] _resources;

    private readonly Subject<(string Name, long Throughput)[]> _updates = new ();
    private (string Name, long Throughput)[] _prev;
    public IObservable<(string Name, long Throughput)[]> Updates => _updates;


    private readonly SourceCache<CPUDisplayVM, ulong> _tasks = new(x => x.ID);
    public readonly ReadOnlyObservableCollection<CPUDisplayVM> _tasksFiltered;
    private readonly CompositeDisposable _compositeDisposable;
    private readonly ILogger<ResourceMonitor> _logger;
    private DateTime _lastMeasuredDateTime;
    public ReadOnlyObservableCollection<CPUDisplayVM> Tasks => _tasksFiltered;

    public ResourceMonitor(ILogger<ResourceMonitor> logger, IEnumerable<IResource> resources)
    {
        _logger = logger;
        _compositeDisposable = new CompositeDisposable();
        _resources = resources.ToArray();
        _lastMeasuredDateTime = DateTime.Now;
        _prev = _resources.Select(x => (x.Name, (long)0)).ToArray();

        RxApp.MainThreadScheduler.ScheduleRecurringAction(_pollInterval, Elapsed)
            .DisposeWith(_compositeDisposable);
        
        _tasks.Connect()
            .Filter(x => x.IsWorking)
            .Bind(out _tasksFiltered)
            .Subscribe()
            .DisposeWith(_compositeDisposable);
    }

    private void Elapsed()
    {
        var elapsedTime = DateTime.Now - _lastMeasuredDateTime;
        var current = _resources.Select(x => (x.Name, x.StatusReport.Transferred)).ToArray();
        var diff = _prev.Zip(current)
            .Select(t => (t.First.Name, (long)((t.Second.Transferred - t.First.Throughput) / elapsedTime.TotalSeconds)))
            .ToArray();
        _prev = current;
        _updates.OnNext(diff);

        _tasks.Edit(l =>
        {
            var used = new HashSet<ulong>();
            var now = DateTime.Now;
            foreach (var resource in _resources)
            {
                foreach (var job in resource.Jobs.Where(j => j.Current > 0))
                {
                    used.Add(job.ID);
                    var tsk = l.Lookup(job.ID);
                    var jobProgress = job.Size == 0 ? Percent.Zero : Percent.FactoryPutInRange(job.Current, (long)job.Size);
                    // Update
                    if (tsk != Optional<CPUDisplayVM>.None)
                    {
                        var t = tsk.Value;
                        t.Msg = job.Description;
                        t.ProgressPercent = jobProgress;
                        t.IsWorking = job.Current > 0;
                    }

                    // Create
                    else
                    {
                        var vm = new CPUDisplayVM
                        {
                            ID = job.ID,
                            StartTime = now,
                            Msg = job.Description,
                            ProgressPercent = jobProgress,
                            IsWorking = job.Current > 0,
                        };
                        l.AddOrUpdate(vm);
                    }
                }
            }
            
            // Delete
            foreach (var itm in l.Items.Where(v => !used.Contains(v.ID)))
                l.Remove(itm);
        });
        _lastMeasuredDateTime = DateTime.Now;
    }

    public void Dispose()
    {
        _compositeDisposable.Dispose();
    }
}