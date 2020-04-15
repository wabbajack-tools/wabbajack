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

        public AbsolutePath Location { get; }

        [Reactive]
        public Percent ProgressPercent { get; private set; }

        [Reactive]
        public bool IsBroken { get; private set; }

        [Reactive]
        public IErrorResponse Error { get; private set; }

        private readonly ObservableAsPropertyHelper<BitmapImage> _Image;
        public BitmapImage Image => _Image.Value;

        private readonly ObservableAsPropertyHelper<bool> _LoadingImage;
        public bool LoadingImage => _LoadingImage.Value;

        public ModListMetadataVM(ModListGalleryVM parent, ModlistMetadata metadata)
        {
            _parent = parent;
            Metadata = metadata;
            Location = Consts.ModListDownloadFolder.Combine(Metadata.Links.MachineURL + (string)Consts.ModListExtension);
            IsBroken = metadata.ValidationSummary.HasFailures;
            OpenWebsiteCommand = ReactiveCommand.Create(() => Utils.OpenWebsite(new Uri($"https://www.wabbajack.org/modlist/{Metadata.Links.MachineURL}")));
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
                            var success = await Download();
                            if (!success)
                            {
                                Error = ErrorResponse.Fail("Download was marked unsuccessful");
                                return false;
                            }
                        }
                        catch (Exception ex)
                        {
                            Error = ErrorResponse.Fail(ex);
                            return false;
                        }
                        // Return an updated check on exists
                        return Location.Exists;
                    }
                    return exists;
                })
                .Where(exists => exists)
                // Do any install page swap over on GUI thread
                .ObserveOnGuiThread()
                .Select(_ =>
                {
                    _parent.MWVM.OpenInstaller(Location);

                    // Wait for modlist member to be filled, then open its readme
                    return _parent.MWVM.Installer.Value.WhenAny(x => x.ModList)
                        .NotNull()
                        .Take(1)
                        .Do(modList =>
                        {
                            try
                            {
                                modList.OpenReadme();
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
                .FlowSwitch(_parent.WhenAny(x => x.IsActive))
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
                .ToGuiProperty(this, nameof(Exists));

            var imageObs = Observable.Return(Metadata.Links.ImageUri)
                .DownloadBitmapImage((ex) => Utils.Log($"Error downloading modlist image {Metadata.Title}"));

            _Image = imageObs
                .ToGuiProperty(this, nameof(Image));

            _LoadingImage = imageObs
                .Select(x => false)
                .StartWith(true)
                .ToGuiProperty(this, nameof(LoadingImage));
        }

        private async Task<bool> Download()
        {
            ProgressPercent = Percent.Zero;
            using (var queue = new WorkQueue(1))
            using (queue.Status.Select(i => i.ProgressPercent)
                .Subscribe(percent => ProgressPercent = percent))
            {
                var tcs = new TaskCompletionSource<bool>();
                queue.QueueTask(async () =>
                {
                    try
                    {
                        var downloader = DownloadDispatcher.ResolveArchive(Metadata.Links.Download);
                        var result = await downloader.Download(new Archive(state: null!) { Name = Metadata.Title, Size = Metadata.DownloadMetadata?.Size ?? 0 }, Location);
                        // Want to rehash to current file, even if failed?
                        Location.FileHashCached();
                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                });


                Task.Run(() => Metrics.Send(Metrics.Downloading, Metadata.Title))
                    .FireAndForget(ex => Utils.Error(ex, "Error sending download metric"));

                return await tcs.Task;
            }
        }
    }
}
