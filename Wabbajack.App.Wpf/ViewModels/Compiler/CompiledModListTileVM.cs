using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Compiler;
using Wabbajack.Messages;
using Wabbajack.Models;

namespace Wabbajack;

public class CompiledModListTileVM
{
    private ILogger _logger;
    public LoadingLock LoadingImageLock { get; } = new();
    public ICommand CompileModListCommand { get; set; }
    [Reactive]
    public CompilerSettings CompilerSettings { get; set; }

    public CompiledModListTileVM(ILogger logger, CompilerSettings compilerSettings)
    {
        _logger = logger;
        CompilerSettings = compilerSettings;
        CompileModListCommand = ReactiveCommand.Create(CompileModList);
    }

    private void CompileModList()
    {
        _logger.LogInformation($"Selected modlist {CompilerSettings.ModListName} for compilation, located in '{CompilerSettings.Source}'");
        NavigateToGlobal.Send(ScreenType.CompilerMain);
        LoadCompilerSettings.Send(CompilerSettings);
    }
}
