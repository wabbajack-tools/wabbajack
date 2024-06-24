using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Paths.IO;
using System.Linq;
using Wabbajack.Networking.WabbajackClientApi;

namespace Wabbajack
{
    public abstract class BaseCompilerVM : BackNavigatingVM
    {
        protected readonly DTOSerializer _dtos;
        protected readonly SettingsManager _settingsManager;
        protected readonly ILogger<BaseCompilerVM> _logger;
        protected readonly Client _wjClient;

        [Reactive] public CompilerSettingsVM Settings { get; set; } = new();

        public BaseCompilerVM(DTOSerializer dtos, SettingsManager settingsManager, ILogger<BaseCompilerVM> logger, Client wjClient) : base(logger)
        {
            _dtos = dtos;
            _settingsManager = settingsManager;
            _logger = logger;
            _wjClient = wjClient;
        }

        protected async Task SaveSettings()
        {
            if (Settings.Source == default || Settings.CompilerSettingsPath == default) return;

            await using var st = Settings.CompilerSettingsPath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(st, Settings.ToCompilerSettings(), _dtos.Options);

            var allSavedCompilerSettings = await _settingsManager.Load<List<AbsolutePath>>(Consts.AllSavedCompilerSettingsPaths);

            // Don't simply remove Settings.CompilerSettingsPath here, because WJ sometimes likes to make default compiler settings files
            allSavedCompilerSettings.RemoveAll(path => path.Parent == Settings.Source);
            allSavedCompilerSettings.Insert(0, Settings.CompilerSettingsPath);

            await _settingsManager.Save(Consts.AllSavedCompilerSettingsPaths, allSavedCompilerSettings);
        }
    }
}
