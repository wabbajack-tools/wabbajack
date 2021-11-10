using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Timers;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Controls;

public class ResourceViewModel : ViewModelBase, IActivatableViewModel, IDisposable
{
    private readonly IResource _resource;
    private readonly Timer _timer;

    public ResourceViewModel(IResource resource)
    {
        Activator = new ViewModelActivator();
        _resource = resource;
        _timer = new Timer(250);

        Name = resource.Name;

        this.WhenActivated(disposables =>
        {
            _timer.Elapsed += TimerElapsed;
            _timer.Start();

            Disposable.Create(() =>
            {
                _timer.Stop();
                _timer.Elapsed -= TimerElapsed;
            }).DisposeWith(disposables);
            
            MaxTasks = _resource.MaxTasks;
            MaxThroughput = _resource.MaxThroughput;
        });
    }

    [Reactive] public int MaxTasks { get; set; }

    [Reactive] public long MaxThroughput { get; set; }

    [Reactive] public long CurrentThroughput { get; set; }

    [Reactive] public string Name { get; set; }
    
    [Reactive] public string ThroughputHumanFriendly { get; set; }


    public void Dispose()
    {
        _timer.Dispose();
    }

    private void TimerElapsed(object? sender, ElapsedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => {
            CurrentThroughput = _resource.StatusReport.Transferred;
            ThroughputHumanFriendly = _resource.StatusReport.Transferred.ToFileSizeString();
        });
    }
}