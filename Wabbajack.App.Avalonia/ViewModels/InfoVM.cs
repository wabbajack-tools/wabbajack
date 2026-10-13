using System;
using System.Reactive.Disposables;
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Interfaces;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Services;

namespace Wabbajack.App.Avalonia.ViewModels;

/// <summary>
///     The info screen: a message, and a Back button to wherever the sender said. WPF built it on
///     BackNavigatingVM, whose own close command it replaced as soon as a message arrived; this is only that part.
/// </summary>
public partial class InfoVM : ViewModel, IClosableVM
{
    public InfoVM(Navigator navigator)
    {
        MessageBus.Current.Listen<LoadInfoScreen>()
            .Subscribe(msg =>
            {
                Info = msg.Info;
                NavigateBackTarget = msg.NavigateBackTarget;
                CloseCommand = ReactiveCommand.Create(() => navigator.NavigateTo(NavigateBackTarget));
            })
            .DisposeWith(CompositeDisposable);
    }

    [Reactive] public partial string Info { get; set; }
    [Reactive] public partial ViewModel NavigateBackTarget { get; set; }
    [Reactive] public partial ICommand CloseCommand { get; private set; }
}
