using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Common;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.ViewModels.Settings;

public class SettingsVM : ViewModelBase
{
    private readonly ILogger<SettingsVM> _logger;
    private readonly IEnumerable<IResource> _resources;
    private readonly ResourceSettingsManager _resourceSettingsManager;
    private readonly Wabbajack.Services.OSIntegrated.Configuration _configuration;

    [Reactive] public ObservableCollection<ResourceSettingVM> ResourceSettings { get; private set; } = new();

    public string Version { get; }

    public ICommand OpenLogFolderCommand { get; }
    public ICommand OpenGitHubCommand { get; }

    public SettingsVM(
        ILogger<SettingsVM> logger,
        IEnumerable<IResource> resources,
        ResourceSettingsManager resourceSettingsManager,
        Wabbajack.Services.OSIntegrated.Configuration configuration)
    {
        _logger = logger;
        _resources = resources;
        _resourceSettingsManager = resourceSettingsManager;
        _configuration = configuration;

        var processLocation = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        var assemblyLocation = typeof(SettingsVM).Assembly.Location;
        var fvi = FileVersionInfo.GetVersionInfo(
            string.IsNullOrWhiteSpace(assemblyLocation) ? processLocation : assemblyLocation);
        Version = fvi.FileVersion ?? "0.0.0.0";

        OpenLogFolderCommand = ReactiveCommand.Create(() => UIUtils.OpenFolder(_configuration.LogLocation));
        OpenGitHubCommand = ReactiveCommand.Create(() =>
            UIUtils.OpenWebsite(new Uri("https://github.com/wabbajack-tools/wabbajack")));

        LoadResourceSettings().FireAndForget();
    }

    private async Task LoadResourceSettings()
    {
        var vms = new ObservableCollection<ResourceSettingVM>();
        foreach (var resource in _resources)
        {
            try
            {
                var setting = await _resourceSettingsManager.GetSetting(resource.Name);
                vms.Add(new ResourceSettingVM(resource, setting, _resourceSettingsManager));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load setting for resource {Name}", resource.Name);
            }
        }

        RxApp.MainThreadScheduler.Schedule(() => ResourceSettings = vms);
    }
}
