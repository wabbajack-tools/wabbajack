using System;
using DynamicData.Kernel;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Messages;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Screens
{
    public class ErrorPageViewModel : ViewModelBase, IActivatableViewModel, IReceiver<Error>
    {
        [Reactive]
        public string ShortMessage { get; set; }
        
        [Reactive]
        public string Prefix { get; set; }
        
        public ErrorPageViewModel()
        {
            Activator = new ViewModelActivator();
        }
        public void Receive(Error val)
        {
            Prefix = val.Prefix;
            ShortMessage = val.Exception.Message;
        }

        public static void Display(string prefix, Exception ex)
        {
            MessageBus.Instance.Send(new Error(prefix, ex));
            MessageBus.Instance.Send(new NavigateTo(typeof(ErrorPageViewModel)));
        }
    }
}