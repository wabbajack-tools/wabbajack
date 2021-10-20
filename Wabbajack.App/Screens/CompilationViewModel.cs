using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Messages;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Screens;

public class CompilationViewModel : ViewModelBase, IReceiverMarker, IReceiver<StartCompilation>
{
    private readonly IServiceProvider _provider;
    private ACompiler _compiler;
    private readonly ILogger<CompilationViewModel> _logger;
    
    [Reactive] public string StatusText { get; set; } = "";
    [Reactive] public Percent StepsProgress { get; set; } = Percent.Zero;
    [Reactive] public Percent StepProgress { get; set; } = Percent.Zero;


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
            _compiler.OnStatusUpdate += (sender, update) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusText = update.StatusText;
                    StepsProgress = update.StepsProgress;
                    StepProgress = update.StepProgress;
                });
            };
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
            StatusText = $"ERRORED: {ex.Message}";
            ErrorPageViewModel.Display("During compilation", ex);
        }
    }
}