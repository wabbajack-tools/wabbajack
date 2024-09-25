using Microsoft.Extensions.Logging;
using Wabbajack.Messages;
using ReactiveUI;
using System.Reactive.Disposables;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Models;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;
using System.Windows.Input;
using System;
using System.Diagnostics;

namespace Wabbajack;

public class CompilerMainVM : BaseCompilerVM, IHasInfoVM
{
    public CompilerDetailsVM CompilerDetailsVM { get; set; }
    public CompilerFileManagerVM CompilerFileManagerVM { get; set; }
    public CompilingVM CompilingVM { get; set; }
    
    [Reactive]
    public CompilerState State { get; set; }
    public LogStream LoggerProvider { get; }

    public ICommand InfoCommand { get; }

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

        InfoCommand = ReactiveCommand.Create(Info);
        
        this.WhenActivated(disposables =>
        {
            State = CompilerState.Configuration;

            this.WhenAnyValue(x => x.State)
                .BindToStrict(CompilingVM, cvm => cvm.State)
                .DisposeWith(disposables);

            Disposable.Empty.DisposeWith(disposables);
        });
    }

    private void Info()
    {
        Process.Start(new ProcessStartInfo("https://wiki.wabbajack.org/modlist_author_documentation/Compilation.html") { UseShellExecute = true });
    }
}
