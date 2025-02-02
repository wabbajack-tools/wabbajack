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

public class PerformanceSettingsVM : ViewModel
{
    public class PerformanceSetting
    {
        [Reactive] public string HumanName { get; set; }
        [Reactive] public long MaxTasks { get; set; }
        [Reactive] public long MaxThroughput { get; set; }
    }

    private readonly Configuration.MainSettings _mainSettings;
    private readonly ResourceSettingsManager _settingsManager;

    private readonly ReadOnlyObservableCollection<PerformanceSetting> _settings;
    public ReadOnlyObservableCollection<PerformanceSetting> Settings => _settings;
    public ObservableCollectionExtended<PerformanceSetting> SourceSettings { get; private set; }
    [Reactive] public int MaxThreads { get; set; }

    public PerformanceSettingsVM(Configuration.MainSettings mainSettings, IResource<DownloadDispatcher> downloadResources, SystemParametersConstructor systemParams, ResourceSettingsManager manager)
    {
        var p = systemParams.Create();

        _mainSettings = mainSettings;
        _settingsManager = manager;
        MaxThreads = Environment.ProcessorCount;

        this.WhenActivated(async disposables =>
        {
            SourceSettings = new ObservableCollectionExtended<PerformanceSetting>((await _settingsManager.GetSettings()).Select((kv) =>
            {
                return new PerformanceSetting()
                {
                    HumanName = kv.Key,
                    MaxTasks = kv.Value.MaxTasks,
                    MaxThroughput = kv.Value.MaxThroughput
                };
            }));

            Disposable.Empty.DisposeWith(disposables);
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
