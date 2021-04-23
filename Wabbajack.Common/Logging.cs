using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Common.StatusFeed.Errors;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

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

        public static async Task InitalizeLogging()
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
                            Log($"Could not delete log at {f}!\n{e}");
                        }
                    });

                    Log($"Deleted {success} log files, failed to delete {failed} logs");
                }
            }
        }


        public static void Log(string msg)
        {
            Log(new GenericInfo(msg));
        }

        public static T Log<T>(T msg) where T : IStatusMessage
        {
            LogStraightToFile(string.IsNullOrWhiteSpace(msg.ExtendedDescription) ? msg.ShortDescription : msg.ExtendedDescription);
            LoggerSubj.OnNext(msg);
            return msg;
        }

        public static void Error(string errMessage)
        {
            Log(errMessage);
        }

        public static void Error(Exception ex, string? extraMessage = null)
        {
            Log(new GenericException(ex, extraMessage));
        }

        public static void ErrorThrow(Exception ex, string? extraMessage = null)
        {
            Error(ex, extraMessage);
            throw ex;
        }

        public static void Error(IException err)
        {
            LogStraightToFile($"{err.ShortDescription}\n{err.Exception.StackTrace}");
            LoggerSubj.OnNext(err);
        }

        public static void ErrorThrow(IException err)
        {
            Error(err);
            throw err.Exception;
        }

        public static void LogStraightToFile(string msg)
        {
            if (!LoggingSettings.LogToFile || LogFile == default) return;
            lock (_logLock)
            {
                File.AppendAllText(LogFile.ToString(), $"{(DateTime.Now - _startTime).TotalSeconds:0.##} - {msg}\r\n", new UTF8Encoding(false, true));
            }
        }

        public static void Status(string msg, Percent progress, bool alsoLog = false)
        {
            WorkQueue.AsyncLocalCurrentQueue.Value?.Report(msg, progress);
            if (alsoLog)
            {
                Utils.Log(msg);
            }
        }

        public static void Status(string msg, bool alsoLog = false)
        {
            Status(msg, Percent.Zero, alsoLog: alsoLog);
        }

        public static void CatchAndLog(Action a)
        {
            try
            {
                a();
            }
            catch (Exception ex)
            {
                Utils.Error(ex);
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
                Utils.Error(ex);
            }
        }

        public static void ErrorMetric(Exception exception)
        {
            throw new NotImplementedException();
        }
    }
}
