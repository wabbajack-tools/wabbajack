using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Util;

namespace Wabbajack;

[JsonName("Mo2ModListInstallerSettings")]
public class Mo2ModlistInstallationSettings
{
    public AbsolutePath InstallationLocation { get; set; }
    public AbsolutePath DownloadLocation { get; set; }
    public bool AutomaticallyOverrideExistingInstall { get; set; }
}

public class PerformanceSettingVM : ViewModel
{
    private readonly ResourceSettingsManager _manager;
    [Reactive] public string HumanName { get; set; }
    [Reactive] public long MaxTasks { get; set; }
    [Reactive] public long MaxThroughput { get; set; }
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

public class PerformanceSettingsVM : ViewModel
{

    private readonly ResourceSettingsManager _settingsManager;

    public SourceList<PerformanceSettingVM> _settings = new();
    public ReadOnlyObservableCollection<PerformanceSettingVM> Settings;
    [Reactive] public int MaxThreads { get; set; }

    public PerformanceSettingsVM(IResource<DownloadDispatcher> downloadResources, SystemParametersConstructor systemParams, ResourceSettingsManager manager)
    {
        var p = systemParams.Create();

        _settingsManager = manager;
        MaxThreads = Environment.ProcessorCount;

        this.WhenActivated(async disposables =>
        {
           var settings = (await _settingsManager.GetSettings()).Select((kv) =>
           {
               return new PerformanceSettingVM(manager)
               {
                   HumanName = kv.Key,
                   MaxTasks = kv.Value.MaxTasks,
                   MaxThroughput = kv.Value.MaxThroughput
               };
           });

            _settings.Edit(s =>
            {
                s.Clear();
                s.AddRange(settings);
            });

            _settings.Connect()
                     .Bind(out Settings)
                     .Subscribe()
                     .DisposeWith(disposables);


        });
    }

}
public class GalleryFilterSettings
{
    public string GameType { get; set; }
    public bool IncludeNSFW { get; set; }
    public bool IncludeUnofficial { get; set; }
    public bool OnlyInstalled { get; set; }
    public string Search { get; set; }
}
