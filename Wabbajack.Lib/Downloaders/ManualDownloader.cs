using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using Syroot.Windows.IO;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;
using File = System.IO.File;

namespace Wabbajack.Lib.Downloaders
{
    public class ManualDownloader : IDownloader
    {
        private FileSystemWatcher _watcher;
        private Subject<FileEvent> _fileEvents = new Subject<FileEvent>();
        private KnownFolder _downloadfolder;

        class FileEvent
        {
            public string FullPath { get; set; }
            public string Name { get; set; }
            public long Size { get; set; }
        }

        public ManualDownloader()
        {
            _downloadfolder = new KnownFolder(KnownFolderType.Downloads);
            _watcher = new FileSystemWatcher(_downloadfolder.Path);
            _watcher.Created += _watcher_Created;
            _watcher.Changed += _watcher_Changed;
        }

        private void _watcher_Changed(object sender, FileSystemEventArgs e)
        {
            PublishEvent(e);
        }
        private void _watcher_Created(object sender, FileSystemEventArgs e)
        {
            PublishEvent(e);
        }

        private void PublishEvent(FileSystemEventArgs e)
        {
            try
            {
                _fileEvents.OnNext(new FileEvent
                {
                    Size = new FileInfo(e.FullPath).Length,
                    Name = e.Name,
                    FullPath = e.FullPath
                });
            }
            catch (IOException)
            {

            }
        }

        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            var url = archive_ini?.General?.manualURL;
            return url != null ? new State { Url = url} : null;
        }

        public void Prepare()
        {
        }

        public class State : AbstractDownloadState
        {
            public string Url { get; set; }
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override void Download(Archive a, string destination)
            {
                var downloader = (ManualDownloader)GetDownloader();
                var abs_path = Path.Combine(downloader._downloadfolder.Path, a.Name);
                lock (downloader)
                {
                    try
                    {
                        Utils.Log($"You must manually visit {Url} and download {a.Name} file by hand.");
                        Utils.Log($"Waiting for {a.Name}");
                        downloader._watcher.EnableRaisingEvents = true;
                        var watcher = downloader._fileEvents
                            .Where(f => f.Size == a.Size)
                            .Where(f => f.FullPath.FileHash(true) == a.Hash)
                            .Buffer(new TimeSpan(0, 5, 0), 1)
                            .Select(x => x.FirstOrDefault())
                            .FirstOrDefaultAsync();
                        Process.Start(Url);
                        
                        abs_path = watcher.Wait()?.FullPath;
                        if (!File.Exists(abs_path))
                            throw new InvalidDataException($"File not found after manual download operation");
                        File.Move(abs_path, destination);
                    }
                    finally
                    {
                        downloader._watcher.EnableRaisingEvents = false;
                    }
                }
            }

            public override bool Verify()
            {
                return true;
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<ManualDownloader>();
            }

            public override string GetReportEntry(Archive a)
            {
                return $"* Manual Download - [{a.Name} - {Url}]({Url})";
            }
        }
    }
}
