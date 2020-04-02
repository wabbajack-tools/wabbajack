using System;
using System.Reactive;
using ReactiveUI;

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
        Uri IconUri { get; }
    }
}
