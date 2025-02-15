using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Compiler;
using Wabbajack.Messages;
using Wabbajack.Models;
using Wabbajack.Paths;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack;

public class CompiledModListTileVM
{
    private ILogger _logger;
    private SettingsManager _settingsManager;
    public LoadingLock LoadingImageLock { get; } = new();
    public ICommand CompileModListCommand { get; }
    public ICommand DeleteModListCommand { get; }
    [Reactive] public CompilerSettings CompilerSettings { get; set; }
    [Reactive] public bool Deleted { get; set; }

    public CompiledModListTileVM(ILogger logger, SettingsManager settingsManager, CompilerSettings compilerSettings)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        CompilerSettings = compilerSettings;
        CompileModListCommand = ReactiveCommand.Create(CompileModList);
        DeleteModListCommand = ReactiveCommand.Create(DeleteModList);
    }

    private async Task<bool> DeleteModList()
    {
        var savedCompilerSettingsPaths = await _settingsManager.Load<List<AbsolutePath>>(Consts.AllSavedCompilerSettingsPaths);
        if (savedCompilerSettingsPaths.RemoveAll(path => path.Parent == CompilerSettings.Source) > 0)
        {
            await _settingsManager.Save(Consts.AllSavedCompilerSettingsPaths, savedCompilerSettingsPaths);
            ReloadCompiledModLists.Send();
            return true;
        }
        return false;
    }

    private void CompileModList()
    {
        _logger.LogInformation($"Selected modlist {CompilerSettings.ModListName} for compilation, located in '{CompilerSettings.Source}'");
        NavigateToGlobal.Send(ScreenType.CompilerMain);
        LoadCompilerSettings.Send(CompilerSettings);
    }
}
