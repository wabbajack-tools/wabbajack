using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Models;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.ViewModels.Compiler;

/// <summary>
/// Create a list: start a new modlist from a Mod Organizer profile, load a saved compiler settings file, or
/// pick up one of the lists compiled before.
/// </summary>
public partial class CompilerHomeVM : ViewModel
{
    /// <summary>The settings key the WPF app keeps the saved compiler settings paths under; shared with it.</summary>
    public const string AllSavedCompilerSettingsPaths = "compiler_settings_paths";

    private readonly SettingsManager _settingsManager;
    private readonly ILogger<CompilerHomeVM> _logger;
    private readonly DTOSerializer _dtos;
    private readonly Navigator _navigator;

    public CompilerHomeVM(ILogger<CompilerHomeVM> logger, SettingsManager settingsManager, DTOSerializer dtos,
        CompilerSettingsInferencer inferencer, FilePicker picker, Navigator navigator)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _dtos = dtos;
        _navigator = navigator;

        MessageBus.Current.Listen<ReloadCompiledModLists>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => LoadAllCompilerSettings().FireAndForget())
            .DisposeWith(CompositeDisposable);

        NewModlistCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await picker.PickFile("Select a Mod Organizer profile (modlist.txt)", ("Modlist", "modlist" + Ext.Txt));
            if (path == default) return;

            try
            {
                var compilerSettings = await inferencer.InferModListFromLocation(path);
                OpenCompiler(compilerSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create new compiler settings for target path {0}! {1}", path, ex.ToString());
            }
        });

        LoadSettingsCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var path = await picker.PickFile("Select a compiler settings file",
                ("Compiler Settings File", "*" + Ext.CompilerSettings));
            if (path == default) return;

            try
            {
                var compilerSettings = _dtos.Deserialize<CompilerSettings>(await File.ReadAllTextAsync(path.ToString()));
                OpenCompiler(compilerSettings!);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load compiler settings from {0}! {1}", path, ex.ToString());
            }
        });

        this.WhenActivated((CompositeDisposable _) => LoadAllCompilerSettings().FireAndForget());
    }

    public ICommand NewModlistCommand { get; }
    public ICommand LoadSettingsCommand { get; }

    [Reactive] public partial ObservableCollection<CompiledModListTileVM> CompiledModLists { get; set; } = new();

    // The compiler screen is not ported yet: this goes to its placeholder, and the settings are sent for
    // whoever listens once it is.
    private void OpenCompiler(CompilerSettings settings)
    {
        _navigator.NavigateTo(ScreenType.CompilerMain);
        LoadCompilerSettings.Send(settings);
    }

    private async Task LoadAllCompilerSettings()
    {
        var lists = new ObservableCollection<CompiledModListTileVM>();
        try
        {
            var paths = await _settingsManager.Load<List<AbsolutePath>>(AllSavedCompilerSettingsPaths);
            foreach (var settingsPath in paths)
            {
                await using var fs = settingsPath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                var settings = (await _dtos.DeserializeAsync<CompilerSettings>(fs))!;
                lists.Add(new CompiledModListTileVM(_logger, _settingsManager, settings, _navigator));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While loading the recently compiled modlists");
        }

        RxApp.MainThreadScheduler.Schedule(() => CompiledModLists = lists);
    }
}

/// <summary>One recently compiled list: its image, name and folder. Clicking opens it; the cross forgets it.</summary>
public partial class CompiledModListTileVM : ReactiveObject
{
    private readonly ILogger _logger;
    private readonly SettingsManager _settingsManager;
    private readonly Navigator _navigator;

    public CompiledModListTileVM(ILogger logger, SettingsManager settingsManager, CompilerSettings compilerSettings,
        Navigator navigator)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _navigator = navigator;
        CompilerSettings = compilerSettings;
        CompileModListCommand = ReactiveCommand.Create(CompileModList);
        DeleteModListCommand = ReactiveCommand.CreateFromTask(DeleteModList);
    }

    public LoadingLock LoadingImageLock { get; } = new();
    public ICommand CompileModListCommand { get; }
    public ICommand DeleteModListCommand { get; }
    public CompilerSettings CompilerSettings { get; }

    /// <summary>Forgets the list: its folder comes out of the saved paths. Nothing on disk is touched.</summary>
    private async Task DeleteModList()
    {
        var paths = await _settingsManager.Load<List<AbsolutePath>>(CompilerHomeVM.AllSavedCompilerSettingsPaths);
        if (paths.RemoveAll(path => path.Parent == CompilerSettings.Source) > 0)
        {
            await _settingsManager.Save(CompilerHomeVM.AllSavedCompilerSettingsPaths, paths);
            ReloadCompiledModLists.Send();
        }
    }

    private void CompileModList()
    {
        _logger.LogInformation("Selected modlist {Name} for compilation, located in '{Source}'",
            CompilerSettings.ModListName, CompilerSettings.Source);
        _navigator.NavigateTo(ScreenType.CompilerMain);
        LoadCompilerSettings.Send(CompilerSettings);
    }
}
