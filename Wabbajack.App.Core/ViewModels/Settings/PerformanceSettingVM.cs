using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack;

public partial class PerformanceSettingVM : ViewModel
{
    private readonly ResourceSettingsManager _manager;
    [Reactive] public partial string HumanName { get; set; }
    [Reactive] public partial long MaxTasks { get; set; }
    [Reactive] public partial long MaxThroughput { get; set; }
    public PerformanceSettingVM(ResourceSettingsManager manager) {
        _manager = manager;

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.MaxTasks, x => x.MaxThroughput)
                .Throttle(TimeSpan.FromSeconds(0.5))
                .Subscribe(async mt =>
                {
                    var setting = new ResourceSettingsManager.ResourceSetting()
                    {
                        MaxTasks = mt.Item1,
                        MaxThroughput = mt.Item2
                    };
                    await manager.SetSetting(HumanName, setting);
                })
                .DisposeWith(disposables);
        });
    }
}
