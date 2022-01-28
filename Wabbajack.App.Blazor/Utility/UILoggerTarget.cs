using System;
using System.Reactive.Subjects;
using NLog;
using NLog.Targets;

namespace Wabbajack.App.Blazor.Utility;

public class UiLoggerTarget : TargetWithLayout
{
    private readonly Subject<string> _logs = new();
    public IObservable<string> Logs => _logs;

    protected override void Write(LogEventInfo logEvent)
    {
        _logs.OnNext(RenderLogEvent(Layout, logEvent));
    }
}
