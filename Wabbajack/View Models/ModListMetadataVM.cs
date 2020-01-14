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
using System.Windows.Media.Imaging;
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

        [Reactive]
        public bool IsBroken { get; private set; }

        [Reactive]
        public IErrorResponse Error { get; private set; }

        private readonly ObservableAsPropertyHelper<BitmapImage> _Image;
        public BitmapImage Image => _Image.Value;

        public ModListMetadataVM(ModListGalleryVM parent, ModlistMetadata metadata)
        {
            _parent = parent;
            Metadata = metadata;
            IsBroken = metadata.ValidationSummary.HasFailures;
            OpenWebsiteCommand = ReactiveCommand.Create(() => Process.Start($"https://www.wabbajack.org/modlist/{Metadata.Links.MachineURL}"));
            ExecuteCommand = ReactiveCommand.CreateFromObservable<Unit, Unit>(
                canExecute: this.WhenAny(x => x.IsBroken).Select(x => !x),
                execute: (unit) => 
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
                        try
                        {
                            await Download();
                        }
                        catch (Exception ex)
                        {
                            Error = ErrorResponse.Fail(ex);
                            return false;
                        }
                        // Return an updated check on exists
                        return File.Exists(Location);
                    }
                    return exists;
                })
                .Where(exists => exists)
                // Do any install page swap over on GUI thread
                .ObserveOnGuiThread()
                .Select(_ =>
                {
                    _parent.MWVM.OpenInstaller(Path.GetFullPath(Location));

                    // Wait for modlist member to be filled, then open its readme
                    return _parent.MWVM.Installer.Value.WhenAny(x => x.ModList)
                        .NotNull()
                        .Take(1)
                        .Do(modList =>
                        {
                            try
                            {
                                modList.OpenReadmeWindow();
                            }
                            catch (Exception ex)
                            {
                                Utils.Error(ex);
                            }
                        });
                })
                .Switch()
                .Unit());

            _Exists = Observable.Interval(TimeSpan.FromSeconds(0.5))
                .Unit()
                .StartWith(Unit.Default)
                .Select(_ =>
                {
                    try
                    {
                        return !metadata.NeedsDownload(Location);
                    }
                    catch (Exception)
                    {
                        return true;
                    }
                })
                .ToProperty(this, nameof(Exists));

            _Image = Observable.Return(Metadata.Links.ImageUri)
                .DownloadBitmapImage((ex) => Utils.Log($"Error downloading modlist image {Metadata.Title}"))
                .ToProperty(this, nameof(Image));
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
