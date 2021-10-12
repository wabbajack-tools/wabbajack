using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.App.Messages;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.Compiler;

namespace Wabbajack.App.Screens;

public class CompilationViewModel : ViewModelBase, IReceiverMarker, IReceiver<StartCompilation>
{
    private readonly IServiceProvider _provider;
    private ACompiler _compiler;
    private readonly ILogger<CompilationViewModel> _logger;

    public CompilationViewModel(ILogger<CompilationViewModel> logger, IServiceProvider provider)
    {
        _logger = logger;
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
        Compile().FireAndForget();
    }

    public async Task Compile()
    {
        try
        {
            await _compiler.Begin(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "During Compilation: {Message}", ex.Message);
        }
    }
}