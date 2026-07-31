using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.CLI.Verbs;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Messages;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack;

public partial class CompilerHomeVM : ViewModel
{
    private readonly SettingsManager _settingsManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CompilerHomeVM> _logger;
    private readonly DTOSerializer _dtos;
    private readonly CompilerSettingsInferencer _inferencer;

    [Reactive] public partial ICommand NewModlistCommand { get; set; }
    [Reactive] public partial ICommand LoadSettingsCommand { get; set; }

    [Reactive] public partial ObservableCollection<CompiledModListTileVM> CompiledModLists { get; set; }

    public FilePickerVM CompilerSettingsPicker { get; private set; }
    public FilePickerVM NewModlistPicker { get; private set; }

    public CompilerHomeVM(ILogger<CompilerHomeVM> logger, SettingsManager settingsManager,
        IServiceProvider serviceProvider, DTOSerializer dtos, CompilerSettingsInferencer inferencer)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _serviceProvider = serviceProvider;
        _dtos = dtos;
        _inferencer = inferencer;

        MessageBus.Current.Listen<ReloadCompiledModLists>()
            .Subscribe(m => LoadAllCompilerSettings().FireAndForget())
            .DisposeWith(CompositeDisposable);

        NewModlistPicker = new FilePickerVM
        {
            ExistCheckOption = FilePickerVM.CheckOptions.On,
            PathType = FilePickerVM.PathTypeOptions.File,
            PromptTitle = "Select a Mod Organizer profile (modlist.txt)"
        };
        // TODO(avalonia-filepicker): FilePickerVM.Filters is still typed as
        // SourceList<CommonFileDialogFilter> (Microsoft.WindowsAPICodePack.Dialogs), which lives in
        // Util/FilePickerVM.cs (out of scope for this file). That type needs to move to the
        // Wabbajack.Abstractions.IFilePicker abstraction before these call sites can drop
        // CommonFileDialogFilter; kept as-is here to stay compilable against the current FilePickerVM.
        NewModlistPicker.Filters.AddRange([
            ("Modlist", "modlist" + Ext.Txt)
        ]);

        CompilerSettingsPicker = new FilePickerVM
        {
            ExistCheckOption = FilePickerVM.CheckOptions.On,
            PathType = FilePickerVM.PathTypeOptions.File,
            PromptTitle = "Select a compiler settings file"
        };
        // TODO(avalonia-filepicker): same CommonFileDialogFilter dependency as above (see FilePickerVM).
        CompilerSettingsPicker.Filters.AddRange([
            ("Compiler Settings File", "*" + Ext.CompilerSettings)
        ]);

        // Awaited, not fired-and-checked: ICommand.Execute returns as soon as the dialog opens, so
        // TargetPath was still empty when it was tested and both entry points did nothing at all.
        NewModlistCommand = ReactiveCommand.CreateFromTask(async () => {
            await NewModlistPicker.PickTargetPathAsync();
            if(NewModlistPicker.TargetPath != default)
            {
                try
                {
                    var compilerSettings = await _inferencer.InferModListFromLocation(NewModlistPicker.TargetPath);
                    NavigateToGlobal.Send(ScreenType.CompilerMain);
                    LoadCompilerSettings.Send(compilerSettings);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to create new compiler settings for target path {0}! {1}", NewModlistPicker.TargetPath, ex.ToString());
                }
            }
        });

        LoadSettingsCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await CompilerSettingsPicker.PickTargetPathAsync();
            if(CompilerSettingsPicker.TargetPath != default)
            {
                try
                {
                    var compilerSettings = _dtos.Deserialize<CompilerSettings>(File.ReadAllText(CompilerSettingsPicker.TargetPath.ToString()));
                    NavigateToGlobal.Send(ScreenType.CompilerMain);
                    LoadCompilerSettings.Send(compilerSettings);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to load compiler settings from {0}! {1}", CompilerSettingsPicker.TargetPath, ex.ToString());
                }
            }
        });

        this.WhenActivated(disposables =>
        {
            LoadAllCompilerSettings().DisposeWith(disposables);
        });
    }

    private async Task LoadAllCompilerSettings()
    {
        CompiledModLists = new();
        var savedCompilerSettingsPaths = await _settingsManager.Load<List<AbsolutePath>>(Consts.AllSavedCompilerSettingsPaths);
        foreach(var settingsPath in savedCompilerSettingsPaths)
        {
            await using var fs = settingsPath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            var settings = (await _dtos.DeserializeAsync<CompilerSettings>(fs))!;
            CompiledModLists.Add(new CompiledModListTileVM(_logger, _settingsManager, settings));
        }
    }
}
