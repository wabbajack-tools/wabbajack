using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Wabbajack.Common
{
    public static class WJFileWatcher
    {
        public enum FileEventType
        {
            Created,
            Changed,
            Deleted
        }

        public static IObservable<(FileEventType, FileSystemEventArgs)> AppLocalEvents { get; }

        static WJFileWatcher()
        {
            var watcher = new FileSystemWatcher((string)Consts.LocalAppDataPath);
            AppLocalEvents = Observable.Merge(
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => watcher.Changed += h, h => watcher.Changed -= h).Select(e => (FileEventType.Changed, e.EventArgs)),
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => watcher.Created += h, h => watcher.Created -= h).Select(e => (FileEventType.Created, e.EventArgs)),
                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => watcher.Deleted += h, h => watcher.Deleted -= h).Select(e => (FileEventType.Deleted, e.EventArgs)))
                .ObserveOn(Scheduler.Default);

            watcher.EnableRaisingEvents = true;
        }
    }
}
