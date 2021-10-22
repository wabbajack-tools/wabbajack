using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Timers;using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.ViewModels;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Controls;

public class ResourceViewModel : ViewModelBase, IActivatableViewModel, IDisposable
{
    private readonly IResource _resource;
    private readonly Timer _timer;
    
    [Reactive]
    public int MaxTasks { get; set; }
    
    [Reactive]
    public long MaxThroughput { get; set; }
    
    [Reactive]
    public long CurrentThroughput { get; set; }
    
    [Reactive]
    public string Name { get; set; }

    public ResourceViewModel(IResource resource)
    {
        Activator = new ViewModelActivator();
        _resource = resource;
        _timer = new Timer(1.0);

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

            this.WhenAnyValue(vm => vm.MaxThroughput)
                .Skip(1)
                .Subscribe(v =>
                {
                    _resource.MaxThroughput = MaxThroughput;
                }).DisposeWith(disposables);
            
            this.WhenAnyValue(vm => vm.MaxTasks)
                .Skip(1)
                .Subscribe(v =>
                {
                    _resource.MaxTasks = MaxTasks;
                }).DisposeWith(disposables);

        });
    }

    private void TimerElapsed(object? sender, ElapsedEventArgs e)
    {
        MaxTasks = _resource.MaxTasks;
        MaxThroughput = _resource.MaxThroughput;
        CurrentThroughput = _resource.StatusReport.Transferred;
    }


    public void Dispose()
    {
        _timer.Dispose();
    }
}