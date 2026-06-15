using System;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Test.TestingInfra;

/// <summary>
/// Bridges Microsoft.Extensions.Logging to test output. TUnit captures Console output per test,
/// so writing there attributes log lines to the running test. Replaces the old
/// XunitTestOutputLoggerProvider used by Xunit.DependencyInjection.
/// </summary>
public sealed class TUnitLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TUnitLogger(categoryName);

    public void Dispose() { }

    private sealed class TUnitLogger : ILogger
    {
        private readonly string _category;

        public TUnitLogger(string category) => _category = category;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Console.WriteLine($"[{logLevel}] {_category}: {message}");
            if (exception != null)
                Console.WriteLine(exception);
        }
    }
}
