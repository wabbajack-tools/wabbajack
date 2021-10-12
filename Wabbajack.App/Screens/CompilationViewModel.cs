using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Wabbajack.App.Messages;
using Wabbajack.App.ViewModels;
using Wabbajack.Compiler;

namespace Wabbajack.App.Screens;

public class CompilationViewModel : ViewModelBase, IReceiverMarker, IReceiver<StartCompilation>
{
    private readonly IServiceProvider _provider;
    private ACompiler _compiler;

    public CompilationViewModel(IServiceProvider provider)
    {
        _provider = provider;
        Activator = new ViewModelActivator();
        
    }
    
    public void Receive(StartCompilation val)
    {
        if (val.Settings is MO2CompilerSettings mo2)
        {
            var compiler = _provider.GetService<MO2Compiler>()!;
            compiler.Settings = mo2;
            _compiler = compiler;

        }

        _compiler.Begin(CancellationToken.None);
    }
}