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
    [Reactive] public string HumanName { get; set; }
    [Reactive] public long MaxTasks { get; set; }
    [Reactive] public long MaxThroughput { get; set; }
}

public class PerformanceSettingsVM : ViewModel
{

    private readonly Configuration.MainSettings _mainSettings;
    private readonly ResourceSettingsManager _settingsManager;

    public SourceList<PerformanceSettingVM> _settings = new();
    public IObservableCollection<PerformanceSettingVM> Settings { get; } = new ObservableCollectionExtended<PerformanceSettingVM>();
    [Reactive] public int MaxThreads { get; set; }

    public PerformanceSettingsVM(Configuration.MainSettings mainSettings, IResource<DownloadDispatcher> downloadResources, SystemParametersConstructor systemParams, ResourceSettingsManager manager)
    {
        var p = systemParams.Create();

        _mainSettings = mainSettings;
        _settingsManager = manager;
        MaxThreads = Environment.ProcessorCount;

        this.WhenActivated(async disposables =>
        {
           var settings = (await _settingsManager.GetSettings()).Select((kv) =>
           {
               return new PerformanceSettingVM()
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
            .Bind(Settings)
            .WhenAnyPropertyChanged(nameof(PerformanceSettingVM.MaxTasks))
            .Subscribe(s =>
            {
                Dictionary<string, ResourceSettingsManager.ResourceSetting> settingsDictionary = new();
                foreach (var setting in Settings)
                {
                    settingsDictionary[setting.HumanName] = new ResourceSettingsManager.ResourceSetting()
                    {
                        MaxTasks = setting.MaxTasks,
                        MaxThroughput = setting.MaxThroughput
                    };
                }
                Task.Run(async () => await _settingsManager.SaveSettings(settingsDictionary));
            })
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
