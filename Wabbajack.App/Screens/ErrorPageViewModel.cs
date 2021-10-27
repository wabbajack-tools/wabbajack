using System;
using Avalonia.Controls.Mixins;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Messages;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Screens;

public class ErrorPageViewModel : ViewModelBase
{
    public ErrorPageViewModel()
    {
        Activator = new ViewModelActivator();
        MessageBus.Current.Listen<Error>()
            .Subscribe(Receive)
            .DisposeWith(VMDisposables);
    }

    [Reactive] public string ShortMessage { get; set; }

    [Reactive] public string Prefix { get; set; }

    public void Receive(Error val)
    {
        Prefix = val.Prefix;
        ShortMessage = val.Exception.Message;
    }

    public static void Display(string prefix, Exception ex)
    {
        MessageBus.Current.SendMessage(new Error(prefix, ex));
        MessageBus.Current.SendMessage(new NavigateTo(typeof(ErrorPageViewModel)));
    }
}