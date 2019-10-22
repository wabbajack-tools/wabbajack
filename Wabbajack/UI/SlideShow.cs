using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack
{
    public class SlideShow : ViewModel
    {
        private readonly Random _random;
        private Slide _lastSlide;
        private const bool UseSync = false;
        private const int MaxCacheSize = 10;

        public List<Slide> SlideShowElements { get; set; }

        public Dictionary<string, Slide> CachedSlides { get; }

        public Queue<Slide> SlidesQueue { get; }

        public AppState AppState { get; }

        public BitmapImage NextIcon { get; } = UIUtils.BitmapImageFromResource("Wabbajack.UI.Icons.next.png");
        public BitmapImage WabbajackLogo { get; } = UIUtils.BitmapImageFromResource("Wabbajack.UI.Banner_Dark.png");

        private bool _ShowNSFW;
        public bool ShowNSFW { get => _ShowNSFW; set => this.RaiseAndSetIfChanged(ref _ShowNSFW, value); }

        private bool _GCAfterUpdating = true;
        public bool GCAfterUpdating { get => _GCAfterUpdating; set => this.RaiseAndSetIfChanged(ref _GCAfterUpdating, value); }

        private bool _Enable = true;
        public bool Enable { get => _Enable; set => this.RaiseAndSetIfChanged(ref _Enable, value); }

        private BitmapImage _Image;
        public BitmapImage Image { get => _Image; set => this.RaiseAndSetIfChanged(ref _Image, value); }

        private string _ModName = "Wabbajack";
        public string ModName { get => _ModName; set => this.RaiseAndSetIfChanged(ref _ModName, value); }

        private string _AuthorName = "Halgari & the Wabbajack Team";
        public string AuthorName { get => _AuthorName; set => this.RaiseAndSetIfChanged(ref _AuthorName, value); }

        private string _Summary;
        public string Summary { get => _Summary; set => this.RaiseAndSetIfChanged(ref _Summary, value); }

        private string _NexusSiteURL = "https://github.com/wabbajack-tools/wabbajack";
        public string NexusSiteURL { get => _NexusSiteURL; set => this.RaiseAndSetIfChanged(ref _NexusSiteURL, value); }

        public IReactiveCommand SlideShowNextItemCommand { get; } = ReactiveCommand.Create(() => { });
        public IReactiveCommand VisitNexusSiteCommand { get; }

        public SlideShow(AppState appState)
        {
            SlideShowElements = NexusApiClient.CachedSlideShow.ToList();
            CachedSlides = new Dictionary<string, Slide>();
            SlidesQueue = new Queue<Slide>();
            _random = new Random();
            AppState = appState;

            this.VisitNexusSiteCommand = ReactiveCommand.Create(
                execute: () => Process.Start(this.NexusSiteURL),
                canExecute: this.WhenAny(x => x.NexusSiteURL)
                    .Select(x => x?.StartsWith("https://") ?? false)
                    .ObserveOnGuiThread());

            // Apply modlist properties when it changes
            this.WhenAny(x => x.AppState.ModList)
                .NotNull()
                .Subscribe(modList =>
                {
                    this.NexusSiteURL = modList.Website;
                    this.ModName = modList.Name;
                    this.AuthorName = modList.Author;
                    this.Summary = modList.Description;
                })
                .DisposeWith(this.CompositeDisposable);

            // Update splashscreen when modlist changes
            Observable.CombineLatest(
                    (this).WhenAny(x => x.AppState.ModList),
                    (this).WhenAny(x => x.AppState.ModListPath),
                    (this).WhenAny(x => x.Enable),
                    (modList, modListPath, enabled) => (modList, modListPath, enabled))
                // Do any potential unzipping on a background thread
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(u =>
                {
                    if (u.enabled
                        && u.modList != null
                        && u.modListPath != null
                        && File.Exists(u.modListPath)
                        && !string.IsNullOrEmpty(u.modList.Image)
                        && u.modList.Image.Length == 36)
                    {
                        try
                        {
                            using (var fs = new FileStream(u.modListPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
                            using (var ms = new MemoryStream())
                            {
                                var entry = ar.GetEntry(u.modList.Image);
                                using (var e = entry.Open())
                                    e.CopyTo(ms);
                                var image = new BitmapImage();
                                image.BeginInit();
                                image.CacheOption = BitmapCacheOption.OnLoad;
                                image.StreamSource = ms;
                                image.EndInit();
                                image.Freeze();

                                return image;
                            }
                        }
                        catch (Exception)
                        {
                            this.AppState.LogMsg("Error loading splash image.");
                        }
                    }
                    return this.WabbajackLogo;
                })
                .ObserveOn(RxApp.MainThreadScheduler)
                .StartWith(this.WabbajackLogo)
                .Subscribe(bitmap => this.Image = bitmap)
                .DisposeWith(this.CompositeDisposable);

            /// Wire slideshow updates
            // Merge all the sources that trigger a slideshow update
            Observable.Merge(
                    // If the natural timer fires
                    Observable.Interval(TimeSpan.FromSeconds(10)).Unit(),
                    // If user requests one manually
                    this.SlideShowNextItemCommand.StartingExecution())
                // When enabled, fire an initial signal
                .StartWith(Unit.Default)
                // Only subscribe to slideshow triggers if enabled and installing
                .FilterSwitch(
                    Observable.CombineLatest(
                        this.WhenAny(x => x.Enable),
                        this.WhenAny(x => x.AppState.Installing),
                        resultSelector: (enabled, installing) => enabled && installing))
                // Don't ever update more than once every half second.  ToDo: Update to debounce
                .Throttle(TimeSpan.FromMilliseconds(500), RxApp.MainThreadScheduler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => this.UpdateSlideShowItem())
                .DisposeWith(this.CompositeDisposable);
        }

        public void PreloadSlideShow()
        {
            var turns = 0;
            for (var i = 0; i < SlideShowElements.Count; i++)
            {
                if (turns >= 3)
                    break;

                if (QueueRandomSlide(true, false))
                    turns++;
            }
        }

        public void UpdateSlideShowItem()
        {
            if (SlidesQueue.Count == 0) return;
            var slide = SlidesQueue.Peek();

            while (CachedSlides.Count >= MaxCacheSize)
            {
                var idx = _random.Next(0, SlideShowElements.Count);
                var randomSlide = SlideShowElements[idx];
                while (!CachedSlides.ContainsKey(randomSlide.ModID) || SlidesQueue.Contains(randomSlide))
                {
                    idx = _random.Next(0, SlideShowElements.Count);
                    randomSlide = SlideShowElements[idx];
                }

                //if (SlidesQueue.Contains(randomSlide)) continue;
                CachedSlides.Remove(randomSlide.ModID);
                if (this.GCAfterUpdating)
                    GC.Collect();
            }

            if (!slide.IsNSFW || (slide.IsNSFW && ShowNSFW))
            {
                this.Image = AppState._noneImage;
                if (slide.ImageURL != null && slide.Image != null)
                {
                    if (!CachedSlides.ContainsKey(slide.ModID)) return;
                    this.Image = slide.Image;
                }

                this.ModName = slide.ModName;
                this.AuthorName = slide.ModAuthor;
                this.Summary = slide.ModDescription;
                this.NexusSiteURL = slide.ModURL;
            }

            SlidesQueue.Dequeue();
            QueueRandomSlide(false, true);
        }

        private void CacheImage(Slide slide)
        {
            Utils.LogToFile($"Caching slide for {slide.ModName} at {slide.ImageURL}");
            using (var ms = new MemoryStream())
            {
                try
                {
                    if (UseSync)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            using (var stream = new HttpClient().GetStreamSync(slide.ImageURL))
                                stream.CopyTo(ms);
                        });
                    }
                    else
                    {
                        using (Task<Stream> stream = new HttpClient().GetStreamAsync(slide.ImageURL))
                        {
                            stream.Wait();
                            stream.Result.CopyTo(ms);
                        }
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = ms;
                        image.EndInit();
                        image.Freeze();

                        slide.Image = image;
                    });
                }
                catch (Exception e)
                {
                    Utils.LogToFile($"Exception while caching slide {slide.ModName} ({slide.ModID})\n{e.ExceptionToString()}");
                }
            }
        }

        private bool QueueRandomSlide(bool init, bool checkLast)
        {
            var result = false;
            var idx = _random.Next(0, SlideShowElements.Count);
            var element = SlideShowElements[idx];

            if (checkLast && SlideShowElements.Count > 1)
            {
                while (element == _lastSlide && (!element.IsNSFW || (element.IsNSFW && ShowNSFW)))
                {
                    idx = _random.Next(0, SlideShowElements.Count);
                    element = SlideShowElements[idx];
                }
            }

            if (element.ImageURL == null)
            {
                if (!init) SlidesQueue.Enqueue(element);
            }
            else
            {
                if (!CachedSlides.ContainsKey(element.ModID))
                {
                    CacheImage(element);
                    CachedSlides.Add(element.ModID, element);
                    SlidesQueue.Enqueue(element);
                    result = true;
                }
                else
                {
                    if(!init) SlidesQueue.Enqueue(element);
                }

                _lastSlide = element;
            }

            return result;
        }
    }
}