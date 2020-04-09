using System;
using System.Reactive;
using System.Security;
using ReactiveUI;
#nullable enable

namespace Wabbajack.Lib.Downloaders
{
    public interface INeedsLogin
    {
        ReactiveCommand<Unit, Unit> TriggerLogin { get; }
        ReactiveCommand<Unit, Unit> ClearLogin { get; }
        IObservable<bool> IsLoggedIn { get; }
        string SiteName { get; }
        IObservable<string> MetaInfo { get; }
        Uri SiteURL { get; }
        Uri? IconUri { get; }
    }

    public struct LoginReturnMessage
    {
        public string Message;
        public bool Failure;

        public LoginReturnMessage(string message, bool failure)
        {
            Message = message;
            Failure = failure;
        }
    }

    public interface INeedsLoginCredentials : INeedsLogin
    {
        LoginReturnMessage LoginWithCredentials(string username, SecureString password);
    }
}
