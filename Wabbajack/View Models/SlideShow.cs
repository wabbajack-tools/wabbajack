using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Media.Imaging;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack
{
    public class SlideShow : ViewModel
    {
        private readonly Random _random = new Random();

        public InstallerVM Installer { get; }

        [Reactive]
        public bool ShowNSFW { get; set; }

        [Reactive]
        public bool Enable { get; set; } = true;

        private readonly ObservableAsPropertyHelper<BitmapImage> _Image;
        public BitmapImage Image => _Image.Value;

        private readonly ObservableAsPropertyHelper<ModVM> _TargetMod;
        public ModVM TargetMod => _TargetMod.Value;

        public IReactiveCommand SlideShowNextItemCommand { get; } = ReactiveCommand.Create(() => { });
        public IReactiveCommand VisitNexusSiteCommand { get; }

        public SlideShow(InstallerVM appState)
        {
            Installer = appState;

            // Wire target slideshow index
            var intervalSeconds = 10;
            // Compile all the sources that trigger a slideshow update, any of which trigger a counter update
            var selectedIndex = Observable.Merge(
                    // If user requests one manually
                    SlideShowNextItemCommand.StartingExecution(),
                    // If the natural timer fires
                    Observable.Merge(
                            // Start with an initial timer
                            Observable.Return(Observable.Interval(TimeSpan.FromSeconds(intervalSeconds))),
                            // but reset timer if user requests one
                            SlideShowNextItemCommand.StartingExecution()
                                .Select(_ => Observable.Interval(TimeSpan.FromSeconds(intervalSeconds))))
                        // When a new timer comes in, swap to it
                        .Switch()
                        .Unit())
                // When filter switch enabled, fire an initial signal
                .StartWith(Unit.Default)
                // Only subscribe to slideshow triggers if enabled and installing
                .FilterSwitch(
                    Observable.CombineLatest(
                        this.WhenAny(x => x.Enable),
                        this.WhenAny(x => x.Installer.Installing),
                        resultSelector: (enabled, installing) => enabled && installing))
                // Block spam
                .Debounce(TimeSpan.FromMilliseconds(250))
                .Scan(
                    seed: 0,
                    accumulator: (i, _) => i + 1)
                .Publish()
                .RefCount();

            // Dynamic list changeset of mod VMs to display
            var modVMs =  this.WhenAny(x => x.Installer.ModList)
                // Whenever modlist changes, grab the list of its slides
                .Select(modList =>
                {
                    if (modList == null)
                    {
                        return Observable.Empty<ModVM>()
                            .ToObservableChangeSet(x => x.ModID);
                    }
                    return modList.SourceModList.Archives
                        .Select(m => m.State)
                        .OfType<NexusDownloader.State>()
                        .Select(nexus => new ModVM(nexus))
                        // Shuffle it
                        .Shuffle(_random)
                        .AsObservableChangeSet(x => x.ModID);
                })
                // Switch to the new list after every modlist change
                .Switch()
                // Filter out any NSFW slides if we don't want them
                .AutoRefreshOnObservable(slide => this.WhenAny(x => x.ShowNSFW))
                .Filter(slide => !slide.IsNSFW || ShowNSFW)
                .RefCount();

            // Find target mod to display by combining dynamic list with currently desired index
            _TargetMod = Observable.CombineLatest(
                    modVMs.QueryWhenChanged(),
                    selectedIndex,
                    resultSelector: (query, selected) => query.Items.ElementAtOrDefault(selected % query.Count))
                .StartWith(default(ModVM))
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, nameof(TargetMod));

            // Mark interest and materialize image of target mod
            _Image = this.WhenAny(x => x.TargetMod)
                // We want to Switch here, not SelectMany, as we want to hotswap to newest target without waiting on old ones
                .Select(x => x?.ImageObservable ?? Observable.Return(default(BitmapImage)))
                .Switch()
                .ToProperty(this, nameof(Image));

            VisitNexusSiteCommand = ReactiveCommand.Create(
                execute: () => Process.Start(TargetMod.ModURL),
                canExecute: this.WhenAny(x => x.TargetMod.ModURL)
                    .Select(x => x?.StartsWith("https://") ?? false)
                    .ObserveOnGuiThread());

            // ToDo
            // Can maybe add "preload" systems to prep upcoming images
            // This would entail subscribing to modVMs, narrowing it down to Top(X) or Page() somehow.
            // The result would not be used anywhere, just simply expressing interest in those mods'
            // images will implicitly cache them
            //
            // Page would be really clever to use, but it's not exactly right as its "window" won't follow the current index,
            // so at the boundary of a page, the next image won't be cached.  Need like a Page() /w an offset parameter, or something.
        }
    }
}
