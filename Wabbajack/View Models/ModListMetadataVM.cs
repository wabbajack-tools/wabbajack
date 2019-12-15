using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Alphaleonis.Win32.Filesystem;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack
{
    public class ModListMetadataVM : ViewModel
    {
        public ModlistMetadata Metadata { get; }
        private ModListGalleryVM _parent;

        public ICommand OpenWebsiteCommand { get; }
        public ICommand ExecuteCommand { get; }

        private readonly ObservableAsPropertyHelper<bool> _Exists;
        public bool Exists => _Exists.Value;

        public string Location => Path.Combine(Consts.ModListDownloadFolder, Metadata.Links.MachineURL + ExtensionManager.Extension);

        [Reactive]
        public double ProgressPercent { get; private set; }

        public ModListMetadataVM(ModListGalleryVM parent, ModlistMetadata metadata)
        {
            _parent = parent;
            Metadata = metadata;
            OpenWebsiteCommand = ReactiveCommand.Create(() => Process.Start($"https://www.wabbajack.org/modlist/{Metadata.Links.MachineURL}"));
            ExecuteCommand = ReactiveCommand.CreateFromObservable<Unit, bool>((unit) => 
                Observable.Return(unit)
                .WithLatestFrom(
                    this.WhenAny(x => x.Exists),
                    (_, e) => e)
                // Do any download work on background thread
                .ObserveOn(RxApp.TaskpoolScheduler)
                .SelectTask(async (exists) =>
                {
                    if (!exists)
                    {
                        await Download();
                        // Return an updated check on exists
                        return File.Exists(Location);
                    }
                    return exists;
                })
                // Do any install page swap over on GUI thread
                .ObserveOnGuiThread()
                .Do(exists =>
                {
                    if (exists)
                    {
                        _parent.MWVM.OpenInstaller(Path.GetFullPath(Location));
                    }
                }));

            _Exists = Observable.Interval(TimeSpan.FromSeconds(0.5))
                .Unit()
                .StartWith(Unit.Default)
                .Select(_ => File.Exists(Location))
                .ToProperty(this, nameof(Exists));
        }

        private Task Download()
        {
            ProgressPercent = 0d;
            var queue = new WorkQueue(1);
            var sub = queue.Status.Select(i => i.ProgressPercent)
                .Subscribe(percent => ProgressPercent = percent);
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            var metric = Metrics.Send("downloading", Metadata.Title);
            queue.QueueTask(async () =>
            {
                var downloader = DownloadDispatcher.ResolveArchive(Metadata.Links.Download);
                await downloader.Download(new Archive{ Name = Metadata.Title, Size = Metadata.DownloadMetadata?.Size ?? 0}, Location);
                Location.FileHashCached();
                sub.Dispose();
                tcs.SetResult(true);
            });

            return tcs.Task;
        }
    }
}
