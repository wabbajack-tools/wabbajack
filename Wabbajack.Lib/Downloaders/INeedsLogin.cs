using System;
using System.Reactive;
using System.Security;
using ReactiveUI;

namespace Wabbajack.Lib.Downloaders
{
    public interface INeedsLogin
    {
        ReactiveCommand<Unit, Unit> TriggerLogin { get; }
        ReactiveCommand<Unit, Unit> ClearLogin { get; }
        IObservable<bool> IsLoggedIn { get; }
        string SiteName { get; }
        IObservable<string>? MetaInfo { get; }
        Uri SiteURL { get; }
        Uri? IconUri { get; }
    }

    public enum LoginReturnCode
    {
        InternalError = -1,
        Success = 0,
        BadInput = 1,
        BadCredentials = 2,
        NeedsMFA = 3,
    }

    public struct LoginReturnMessage
    {
        public string Message;
        public LoginReturnCode ReturnCode;

        public LoginReturnMessage(string message, LoginReturnCode returnCode)
        {
            Message = message;
            ReturnCode = returnCode;
        }
    }

    public interface INeedsLoginCredentials : INeedsLogin
    {
        LoginReturnMessage LoginWithCredentials(string username, SecureString password, string? mfa);
    }
}
