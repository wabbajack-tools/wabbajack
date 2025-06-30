using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Paths.IO;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Messages;

namespace Wabbajack;

public abstract class BaseCompilerVM : ProgressViewModel
{
    protected readonly DTOSerializer _dtos;
    protected readonly SettingsManager _settingsManager;
    protected readonly ILogger<BaseCompilerVM> _logger;
    protected readonly Client _wjClient;

    [Reactive] public CompilerSettingsVM Settings { get; set; } = new();

    public BaseCompilerVM(DTOSerializer dtos, SettingsManager settingsManager, ILogger<BaseCompilerVM> logger, Client wjClient)
    {
        _dtos = dtos;
        _settingsManager = settingsManager;
        _logger = logger;
        _wjClient = wjClient;

        MessageBus.Current.Listen<LoadCompilerSettings>()
            .Subscribe(msg => {
                var csVm = new CompilerSettingsVM(msg.CompilerSettings);
                Settings = csVm;
            })
            .DisposeWith(CompositeDisposable);
    }

    protected async Task SaveSettings()
    {
        if (Settings.Source == default || Settings.CompilerSettingsPath == default) return;

        try
        {
            await using var st = Settings.CompilerSettingsPath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(st, Settings.ToCompilerSettings(), new JsonSerializerOptions(_dtos.Options) { WriteIndented = true });
        }
        catch(Exception ex)
        {
            _logger.LogError("Failed to save compiler settings to {0}! {1}", Settings.CompilerSettingsPath, ex.ToString());
        }

        var allSavedCompilerSettings = await _settingsManager.Load<List<AbsolutePath>>(Consts.AllSavedCompilerSettingsPaths);

        // Don't simply remove Settings.CompilerSettingsPath here, because WJ sometimes likes to make default compiler settings files
        allSavedCompilerSettings.RemoveAll(path => path.Parent == Settings.Source);
        allSavedCompilerSettings.Insert(0, Settings.CompilerSettingsPath);

        try
        {
            await _settingsManager.Save(Consts.AllSavedCompilerSettingsPaths, allSavedCompilerSettings);
        }
        catch(Exception ex)
        {
            _logger.LogError("Failed to save all saved compiler settings! {0}", ex.ToString());
        }
    }
}
