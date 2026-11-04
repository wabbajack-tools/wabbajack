using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.ViewModels.Settings;

/// <summary>One resource's task limit. Saved half a second after it stops changing, while the row is on screen.</summary>
public partial class PerformanceSettingVM : ViewModel
{
    public PerformanceSettingVM(ResourceSettingsManager manager)
    {
        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.MaxTasks, x => x.MaxThroughput)
                .Throttle(TimeSpan.FromSeconds(0.5))
                .Subscribe(async mt =>
                {
                    var setting = new ResourceSettingsManager.ResourceSetting
                    {
                        MaxTasks = mt.Item1,
                        MaxThroughput = mt.Item2
                    };
                    await manager.SetSetting(HumanName, setting);
                })
                .DisposeWith(disposables);
        });
    }

    [Reactive] public partial string HumanName { get; set; } = "";
    [Reactive] public partial long MaxTasks { get; set; }
    [Reactive] public partial long MaxThroughput { get; set; }
}

/// <summary>The Performance card: every resource the app limits, read when the card is shown.</summary>
public partial class PerformanceSettingsVM : ViewModel
{
    public PerformanceSettingsVM(ResourceSettingsManager manager)
    {
        MaxThreads = Environment.ProcessorCount;

        this.WhenActivated(disposables =>
        {
            manager.GetSettings().ToObservable()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(settings =>
                {
                    Settings.Clear();
                    foreach (var (name, setting) in settings)
                        Settings.Add(new PerformanceSettingVM(manager)
                        {
                            HumanName = name,
                            MaxTasks = setting.MaxTasks,
                            MaxThroughput = setting.MaxThroughput
                        });
                })
                .DisposeWith(disposables);
        });
    }

    public ObservableCollection<PerformanceSettingVM> Settings { get; } = new();

    [Reactive] public partial int MaxThreads { get; set; }
}
