using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SteamKit2.GC.Dota.Internal;
using SteamKit2.Internal;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Messages;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack
{
    public class CompilerHomeVM : ViewModel
    {
        private readonly SettingsManager _settingsManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CompilerHomeVM> _logger;
        private readonly CancellationToken _cancellationToken;
        private readonly DTOSerializer _dtos;

        public ICommand NewModListCommand { get; set; }
        public ICommand LoadSettingsCommand { get; set; }

        [Reactive]
        public ObservableCollection<CompiledModListTileVM> CompiledModLists { get; set; }

        public FilePickerVM CompilerSettingsPicker { get; private set; }

        public CompilerHomeVM(ILogger<CompilerHomeVM> logger, SettingsManager settingsManager,
            IServiceProvider serviceProvider, DTOSerializer dtos)
        {
            _logger = logger;
            _settingsManager = settingsManager;
            _serviceProvider = serviceProvider;
            _dtos = dtos;

            CompilerSettingsPicker = new FilePickerVM
            {
                ExistCheckOption = FilePickerVM.CheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.File,
                PromptTitle = "Select a compiler settings file"
            };
            CompilerSettingsPicker.Filters.AddRange([
                new CommonFileDialogFilter("Compiler Settings File", "*" + Ext.CompilerSettings)
            ]);

            NewModListCommand = ReactiveCommand.Create(() => {
                NavigateToGlobal.Send(ScreenType.CompilerDetails);
                LoadCompilerSettings.Send(new());
            });

            LoadSettingsCommand = ReactiveCommand.Create(() =>
            {
                CompilerSettingsPicker.SetTargetPathCommand.Execute(null);
                if(CompilerSettingsPicker.TargetPath != default)
                {
                    try
                    {
                        var compilerSettings = _dtos.Deserialize<CompilerSettings>(File.ReadAllText(CompilerSettingsPicker.TargetPath.ToString()));
                        NavigateToGlobal.Send(ScreenType.CompilerDetails);
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
                CompiledModLists.Add(new CompiledModListTileVM(_logger, settings));
            }
        }
    }
}
