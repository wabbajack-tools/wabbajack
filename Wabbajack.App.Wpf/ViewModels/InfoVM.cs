using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive.Disposables;
using Wabbajack.Messages;

namespace Wabbajack;

public class InfoVM : BackNavigatingVM
{
    public InfoVM(ILogger<InfoVM> logger) : base(logger)
    {
        MessageBus.Current.Listen<LoadInfoScreen>()
            .Subscribe(msg => {
                Info = msg.Info;
                NavigateBackTarget = msg.NavigateBackTarget;
                CloseCommand = ReactiveCommand.Create(() => NavigateTo.Send(NavigateBackTarget));
            })
            .DisposeWith(CompositeDisposable);
    }
    [Reactive] public string Info { get; set; }
}
