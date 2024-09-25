using Microsoft.Extensions.Logging;
using Wabbajack.Messages;
using ReactiveUI;
using System.Reactive.Disposables;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Models;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack;

public class CompilerMainVM : BaseCompilerVM
{
    public CompilerDetailsVM CompilerDetailsVM { get; set; }
    public CompilerFileManagerVM CompilerFileManagerVM { get; set; }
    public CompilingVM CompilingVM { get; set; }
    
    [Reactive]
    public CompilerState State { get; set; }
    public LogStream LoggerProvider { get; }
    public CompilerMainVM(ILogger<CompilerMainVM> logger, DTOSerializer dtos, SettingsManager settingsManager,
        LogStream loggerProvider, Client wjClient, CompilerDetailsVM compilerDetailsVM, CompilerFileManagerVM compilerFileManagerVM, CompilingVM compilingVM) : base(dtos, settingsManager, logger, wjClient)
    {
        LoggerProvider = loggerProvider;
        CompilerDetailsVM = compilerDetailsVM;
        CompilerFileManagerVM = compilerFileManagerVM;
        CompilingVM = compilingVM;

        BackCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveSettings();
            NavigateToGlobal.Send(ScreenType.Home);
        });
        
        this.WhenActivated(disposables =>
        {
            State = CompilerState.Configuration;

            this.WhenAnyValue(x => x.State)
                .BindToStrict(CompilingVM, cvm => cvm.State)
                .DisposeWith(disposables);

            Disposable.Empty.DisposeWith(disposables);
        });
    }
}
