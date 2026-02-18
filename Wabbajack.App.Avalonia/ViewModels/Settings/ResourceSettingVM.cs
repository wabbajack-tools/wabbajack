using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.ViewModels.Settings;

public class ResourceSettingVM : ViewModelBase
{
    private readonly IResource _resource;
    private readonly ResourceSettingsManager _settingsManager;

    public string Name => _resource.Name;

    [Reactive] public int MaxTasks { get; set; }

    /// <summary>Throughput in MB/s. 0 = unlimited.</summary>
    [Reactive] public long MaxThroughputMB { get; set; }

    public int ProcessorCount { get; } = Environment.ProcessorCount;

    private readonly ObservableAsPropertyHelper<string> _throughputLabel;
    public string ThroughputLabel => _throughputLabel.Value;

    public ResourceSettingVM(IResource resource, ResourceSettingsManager.ResourceSetting setting,
        ResourceSettingsManager manager)
    {
        _resource = resource;
        _settingsManager = manager;

        MaxTasks = (int)Math.Clamp(setting.MaxTasks, 1, 64);
        // Store throughput in MB/s for display (0 = unlimited)
        MaxThroughputMB = setting.MaxThroughput == 0 ? 0 : setting.MaxThroughput / (1024 * 1024);

        _throughputLabel = this.WhenAnyValue(vm => vm.MaxThroughputMB)
            .Select(mb => mb == 0 ? "Unlimited" : $"{mb} MB/s")
            .ToProperty(this, vm => vm.ThroughputLabel);

        this.WhenAnyValue(vm => vm.MaxTasks, vm => vm.MaxThroughputMB)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(500), RxApp.TaskpoolScheduler)
            .Subscribe(_ => Save().FireAndForget());
    }

    private async Task Save()
    {
        _resource.MaxTasks = MaxTasks;
        _resource.MaxThroughput = MaxThroughputMB == 0 ? 0 : MaxThroughputMB * 1024 * 1024;

        await _settingsManager.SetSetting(_resource.Name, new ResourceSettingsManager.ResourceSetting
        {
            MaxTasks = MaxTasks,
            MaxThroughput = _resource.MaxThroughput
        });
    }
}
