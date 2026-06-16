using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Downloaders;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack;

public partial class PerformanceSettingsVM : ViewModel
{

    private readonly ResourceSettingsManager _settingsManager;

    public SourceList<PerformanceSettingVM> _settings = new();
    public ReadOnlyObservableCollection<PerformanceSettingVM> Settings;
    [Reactive] public partial int MaxThreads { get; set; }

    public PerformanceSettingsVM(IResource<DownloadDispatcher> downloadResources, ResourceSettingsManager manager)
    {
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
