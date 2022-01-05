using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using DynamicData;
using Microsoft.Extensions.Logging;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.Models;

public class LoggerProvider : ILoggerProvider
{
    private readonly RelativePath _appName;
    private readonly Configuration _configuration;
    private readonly CompositeDisposable _disposables;
    private readonly Stream _logFile;
    private readonly StreamWriter _logStream;

    public readonly ReadOnlyObservableCollection<ILogMessage> _messagesFiltered;
    private readonly DateTime _startupTime;

    private long _messageId;
    private readonly SourceCache<ILogMessage, long> _messageLog = new(m => m.MessageId);
    private readonly Subject<ILogMessage> _messages = new();

    public LoggerProvider(Configuration configuration)
    {
        _startupTime = DateTime.UtcNow;
        _configuration = configuration;
        _configuration.LogLocation.CreateDirectory();

        _disposables = new CompositeDisposable();

        Messages.Subscribe(m => _messageLog.AddOrUpdate(m))
            .DisposeWith(_disposables);

        Messages.Subscribe(m => LogToFile(m))
            .DisposeWith(_disposables);

        _messageLog.Connect()
            .Bind(out _messagesFiltered)
            .Subscribe()
            .DisposeWith(_disposables);

        _messages.DisposeWith(_disposables);

        _appName = typeof(LoggerProvider).Assembly.Location.ToAbsolutePath().FileName;
        LogPath = _configuration.LogLocation.Combine($"{_appName}.current.log");
        _logFile = LogPath.Open(FileMode.Append, FileAccess.Write);
        _logFile.DisposeWith(_disposables);

        _logStream = new StreamWriter(_logFile, Encoding.UTF8);
    }

    public IObservable<ILogMessage> Messages => _messages;
    public AbsolutePath LogPath { get; }
    public ReadOnlyObservableCollection<ILogMessage> MessageLog => _messagesFiltered;

    public void Dispose()
    {
        _disposables.Dispose();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(this, categoryName);
    }

    private void LogToFile(ILogMessage logMessage)
    {
        var line = $"[{logMessage.TimeStamp - _startupTime}] {logMessage.LongMessage}";
        lock (_logStream)
        {
            _logStream.Write(line);
            _logStream.Flush();
        }
    }

    private long NextMessageId()
    {
        return Interlocked.Increment(ref _messageId);
    }

    public class Logger : ILogger
    {
        private readonly string _categoryName;
        private readonly LoggerProvider _provider;
        private ImmutableList<object> Scopes = ImmutableList<object>.Empty;

        public Logger(LoggerProvider provider, string categoryName)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Debug.WriteLine($"{logLevel} - {formatter(state, exception)}");
            _provider._messages.OnNext(new LogMessage<TState>(DateTime.UtcNow, _provider.NextMessageId(), logLevel,
                eventId, state, exception, formatter));
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
        DateTime TimeStamp { get; }
        string LongMessage { get; }
    }

    private record LogMessage<TState>(DateTime TimeStamp, long MessageId, LogLevel LogLevel, EventId EventId,
        TState State, Exception? Exception, Func<TState, Exception?, string> Formatter) : ILogMessage
    {
        public string ShortMessage => Formatter(State, Exception);

        public string LongMessage
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine(ShortMessage);
                if (Exception != null)
                {
                    sb.Append("Exception: ");
                    sb.Append(Exception);
                }

                return sb.ToString();
            }
        }
    }
}