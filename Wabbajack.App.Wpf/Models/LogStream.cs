using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using NLog;
using NLog.Targets;
using ReactiveUI;

namespace Wabbajack.Models;

public class LogStream : TargetWithLayout
{

    private readonly SourceCache<ILogMessage, long> _messageLog = new(x => x.MessageId);
    private readonly Subject<ILogMessage> _messages = new();
    
    public readonly ReadOnlyObservableCollection<ILogMessage> _messagesFiltered;
    private readonly CompositeDisposable _disposables;
    public ReadOnlyObservableCollection<ILogMessage> MessageLog => _messagesFiltered;
    public IObservable<ILogMessage> Messages => _messages;
    

    public LogStream()
    {
        _disposables = new CompositeDisposable();
        _messageLog.Connect()
            .LimitSizeTo(200)
            .Bind(out _messagesFiltered)
            .Subscribe()
            .DisposeWith(_disposables);

        Messages
            .Subscribe(m =>
            {
                RxApp.MainThreadScheduler.Schedule(m, (_, message) =>
                {
                    _messageLog.AddOrUpdate(message);
                    return Disposable.Empty;
                });
            })
            .DisposeWith(_disposables);

        _messages.DisposeWith(_disposables);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _disposables.Dispose();
    }

    protected override void Write(LogEventInfo logEvent)
    {
        _messages.OnNext(new LogMessage(logEvent));
    }
    public interface ILogMessage
    {
        long MessageId { get; }

        string ShortMessage { get; }
        DateTime TimeStamp { get; }
        LogLevel Level { get; }
    }

    private record LogMessage(LogEventInfo info) : ILogMessage
    {
        public long MessageId => info.SequenceID;
        public string ShortMessage => info.FormattedMessage;
        public DateTime TimeStamp => info.TimeStamp;
        public LogLevel Level => info.Level;
        public string LongMessage => $"[{TimeStamp.ToString("HH:mm:ss")} {info.Level.ToString().ToUpper()}] {info.FormattedMessage}";
    }
    
}