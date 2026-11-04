using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.ViewModels.Compiler;

/// <summary>
/// What the three compiler view models share, as in WPF: each keeps its own <see cref="CompilerSettingsVM" />,
/// replaced whenever a list is loaded, and can write it back to the list's .compiler_settings file.
/// </summary>
public abstract partial class BaseCompilerVM : ProgressViewModel
{
    protected readonly DTOSerializer _dtos;
    protected readonly SettingsManager _settingsManager;
    protected readonly ILogger<BaseCompilerVM> _logger;
    protected readonly Client _wjClient;

    protected BaseCompilerVM(DTOSerializer dtos, SettingsManager settingsManager, ILogger<BaseCompilerVM> logger, Client wjClient)
    {
        _dtos = dtos;
        _settingsManager = settingsManager;
        _logger = logger;
        _wjClient = wjClient;

        MessageBus.Current.Listen<LoadCompilerSettings>()
            .Subscribe(msg => Settings = new CompilerSettingsVM(msg.CompilerSettings))
            .DisposeWith(CompositeDisposable);
    }

    [Reactive] public partial CompilerSettingsVM Settings { get; set; } = new();

    /// <summary>
    ///     Writes the settings next to the list's MO2 folder and puts that file first in the recently compiled
    ///     lists, replacing any other settings file for the same folder.
    /// </summary>
    protected async Task SaveSettings()
    {
        if (Settings.Source == default || Settings.CompilerSettingsPath == default) return;

        try
        {
            await using var st = Settings.CompilerSettingsPath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(st, Settings.ToCompilerSettings(),
                new JsonSerializerOptions(_dtos.Options) { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to save compiler settings to {0}! {1}", Settings.CompilerSettingsPath, ex.ToString());
        }

        var allSavedCompilerSettings =
            await _settingsManager.Load<List<AbsolutePath>>(CompilerHomeVM.AllSavedCompilerSettingsPaths);

        // Don't simply remove Settings.CompilerSettingsPath here, because WJ sometimes likes to make default
        // compiler settings files.
        allSavedCompilerSettings.RemoveAll(path => path.Parent == Settings.Source);
        allSavedCompilerSettings.Insert(0, Settings.CompilerSettingsPath);

        try
        {
            await _settingsManager.Save(CompilerHomeVM.AllSavedCompilerSettingsPaths, allSavedCompilerSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to save all saved compiler settings! {0}", ex.ToString());
        }
    }
}
