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
    public class CreateModListVM : ViewModel
    {
        private readonly SettingsManager _settingsManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CreateModListVM> _logger;
        private readonly CancellationToken _cancellationToken;
        private readonly DTOSerializer _dtos;

        public ICommand NewModListCommand { get; set; }

        [Reactive]
        public ObservableCollection<CreatedModlistVM> CreatedModlists { get; set; }

        public CreateModListVM(ILogger<CreateModListVM> logger, SettingsManager settingsManager,
            IServiceProvider serviceProvider, DTOSerializer dtos)
        {
            _logger = logger;
            _settingsManager = settingsManager;
            _serviceProvider = serviceProvider;
            _dtos = dtos;
            NewModListCommand = ReactiveCommand.Create(() => {
                NavigateToGlobal.Send(ScreenType.Compiler);
                LoadModlistForCompiling.Send(new());
            });
            this.WhenActivated(disposables =>
            {
                LoadAllCompilerSettings().DisposeWith(disposables);
            });
        }

        private async Task LoadAllCompilerSettings()
        {
            CreatedModlists = new();
            var savedCompilerSettingsPaths = await _settingsManager.Load<List<AbsolutePath>>(Consts.AllSavedCompilerSettingsPaths);
            foreach(var settingsPath in savedCompilerSettingsPaths)
            {
                await using var fs = settingsPath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                var settings = (await _dtos.DeserializeAsync<CompilerSettings>(fs))!;
                CreatedModlists.Add(new CreatedModlistVM(_logger, settings));
            }
        }
    }
}
