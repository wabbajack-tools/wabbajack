using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Common.StatusFeed.Errors;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Common
{
    public static class LoggingSettings
    {
        // False by default, so that library users do not have to swap it
        public static bool LogToFile = false;
    }

    public static partial class Utils
    {
        public static AbsolutePath LogFile { get; private set; }
        public static AbsolutePath LogFolder { get; private set; }

        private static object _logLock = new object();

        private static DateTime _startTime;

        private static readonly Subject<IStatusMessage> LoggerSubj = new Subject<IStatusMessage>();
        public static IObservable<IStatusMessage> LogMessages => LoggerSubj;

        public enum LogLevel
        {
            Fatal,
            Error,
            Warn,
            Info,
            Trace
        }

        public static async Task InitializeLogging()
        {
            _startTime = DateTime.Now;

            if (LoggingSettings.LogToFile)
            {
                LogFolder = Consts.LogsFolder;
                LogFile = Consts.LogFile;
                Consts.LocalAppDataPath.CreateDirectory();
                Consts.LogsFolder.CreateDirectory();

                if (LogFile.Exists)
                {
                    var newPath = Consts.LogsFolder.Combine(Consts.EntryPoint.FileNameWithoutExtension + LogFile.LastModified.ToString(" yyyy-MM-dd HH_mm_ss") + ".log");
                    await LogFile.MoveToAsync(newPath, true);
                }

                var logFiles = LogFolder.EnumerateFiles(false).ToList();
                if (logFiles.Count >= Consts.MaxOldLogs)
                {
                    Log($"Maximum amount of old logs reached ({logFiles.Count} >= {Consts.MaxOldLogs})");
                    var filesToDelete = logFiles
                        .Where(f => f.IsFile)
                        .OrderBy(f => f.LastModified)
                        .Take(logFiles.Count - Consts.MaxOldLogs)
                        .ToList();

                    Log($"Found {filesToDelete.Count} old log files to delete");

                    var success = 0;
                    var failed = 0;
                    filesToDelete.Do(f =>
                    {
                        try
                        {
                            f.Delete();
                            success++;
                        }
                        catch (Exception e)
                        {
                            failed++;
                            Warn($"Could not delete log at {f}!\n{e}");
                        }
                    });

                    Log($"Deleted {success} log files, failed to delete {failed} logs");
                }
            }
        }

        public static void Fatal(Exception exception, string? msg = null, bool throwException = true, [CallerFilePath] string caller = "")
        {
            if (exception is IException ex)
            {
                Log(ex, level: LogLevel.Fatal, caller: caller);
                if (throwException) throw ex.Exception;
            }
            else
            {
                Log(new GenericException(exception, msg), LogLevel.Fatal, caller: caller);
                if (throwException) throw exception;
            }
        }

        public static void Error(Exception exception, string? message = null, bool showInLog = true, bool writeToFile = true, [CallerFilePath] string caller = "")
        {
            Log(new GenericException(exception, message), LogLevel.Error, showInLog, writeToFile, caller: caller);
        }

        public static void Error(string msg, bool showInLog = true, bool writeToFile = true, [CallerFilePath] string caller = "")
        {
            Log(new GenericInfo(msg), LogLevel.Error, showInLog, writeToFile, caller);
        }

        public static void Warn(string msg, bool showInLog = true, bool writeToFile = true, [CallerFilePath] string caller = "")
        {
            Log(new GenericInfo(msg), LogLevel.Warn, writeToFile, writeToFile, caller);
        }

        public static void Trace(string msg, [CallerFilePath] string caller = "")
        {
            Log(new GenericInfo(msg), LogLevel.Trace, showInLog: false, caller: caller);
        }

        public static void Log(string msg, bool showInLog = true, bool writeToFile = true, [CallerFilePath] string caller = "")
        {
            Log(new GenericInfo(msg), LogLevel.Info, writeToFile, writeToFile, caller);
        }

        public static T Log<T>(T msg, LogLevel level = LogLevel.Info, bool? showInLog = true, bool? writeToFile = true, [CallerFilePath] string caller = "") where T : IStatusMessage
        {
            if (writeToFile == true) AppendToLogFile(string.IsNullOrWhiteSpace(msg.ExtendedDescription) ? msg.ShortDescription : msg.ExtendedDescription, level, caller: caller);
            if (showInLog == true) LoggerSubj.OnNext(msg);
            return msg;
        }

        private static void AppendToLogFile(string msg, LogLevel level, string caller)
        {
            if (!LoggingSettings.LogToFile || LogFile == default) return;
            lock (_logLock)
            {
                var t = (DateTime.Now - _startTime);
                var formattedTimeSince = string.Format($"[{((int)t.TotalHours):D2}:{t:mm}:{t:ss}.{t:ff}] ");
                // Regex matches last file in a path string, supports "\\", "\", "/" separators.
                Regex regex = new Regex(@"[^\\\/]+(?=[\w]*$)|[^\\\/]+$");
                if (!string.IsNullOrEmpty(caller))
                {
                    var match = regex.Match(caller);
                    if (match.Success) caller = $"[{match.Groups[0].Value}] ";
                }
                string formattedLevel = $"[{level}] ".PadRight(8);
                File.AppendAllText(LogFile.ToString(), $"{formattedTimeSince}{formattedLevel}{caller}{msg}\r\n", new UTF8Encoding(false, true));
            }
        }

        public static void Status(string msg, Percent progress, bool alsoLog = false, LogLevel level = LogLevel.Info, [CallerFilePath] string caller = "")
        {
            WorkQueue.AsyncLocalCurrentQueue.Value?.Report(msg, progress);
            if (alsoLog) Log(msg, caller: caller);
        }

        public static void Status(string msg, bool alsoLog = false, LogLevel level = LogLevel.Info, [CallerFilePath] string caller = "")
        {
            Status(msg, Percent.Zero, alsoLog, level, caller);
        }

        public static void CatchAndLog(Action a)
        {
            try
            {
                a();
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        public static async Task CatchAndLog(Func<Task> f)
        {
            try
            {
                await f();
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }
    }
}
