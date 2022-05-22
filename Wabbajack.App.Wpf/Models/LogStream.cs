using System;
using System.Reactive.Subjects;
using System.Text;
using DynamicData;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

namespace Wabbajack.Models;

public class LogStream : TargetWithLayout
{

    public readonly SourceCache<ILogMessage, long> MessageLog = new(x => x.MessageId);

    protected override void Write(LogEventInfo logEvent)
    {
        MessageLog.AddOrUpdate(new LogMessage(logEvent));
    }
    public interface ILogMessage
    {
        long MessageId { get; }

        string ShortMessage { get; }
        DateTime TimeStamp { get; }
        string LongMessage { get; }
    }

    private record LogMessage(LogEventInfo info) : ILogMessage
    {
        public long MessageId => info.SequenceID;
        public string ShortMessage => info.ToString();
        public DateTime TimeStamp => info.TimeStamp;
        public string LongMessage => info.FormattedMessage;
    }
    
}