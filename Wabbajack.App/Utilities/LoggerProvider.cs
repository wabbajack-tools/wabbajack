using System;
using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Wabbajack.App.Utilities;

public class LoggerProvider : ILoggerProvider
{
    private Subject<ILogMessage> _messages = new();
    public IObservable<ILogMessage> Messages => _messages;

    private long _messageID = 0;

    public long NextMessageId()
    {
        return Interlocked.Increment(ref _messageID);
    }
    
    public void Dispose()
    {
       _messages.Dispose();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(this, categoryName);
    }

    public class Logger : ILogger
    {
        private readonly LoggerProvider _provider;
        private ImmutableList<object> Scopes = ImmutableList<object>.Empty;
        private readonly string _categoryName;

        public Logger(LoggerProvider provider, string categoryName)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _provider._messages.OnNext(new LogMessage<TState>(_provider.NextMessageId(), logLevel, eventId, state, exception, formatter));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            Scopes = Scopes.Add(state);
            return Disposable.Create(() => Scopes = Scopes.Remove(state));
        }
    }

    public interface ILogMessage
    {
        long MessageId { get; }

        string ShortMessage { get; }
    }

    record LogMessage<TState>(long MessageId, LogLevel LogLevel, EventId EventId, TState State, Exception? Exception, Func<TState, Exception?, string> Formatter) : ILogMessage
    {
        public string ShortMessage => Formatter(State, Exception);
    }
}