using ReactiveUI;
using System;
using System.Collections.Generic;
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
using Wabbajack.NexusApi;

namespace Wabbajack.UI
{
    public class Slide
    {
        public Slide(string modName, string modID, string modDescription, string modAuthor, bool isNSFW, string modUrl, string imageURL)
        {
            ModName = modName;
            ModDescription = modDescription;
            ModAuthor = modAuthor;
            IsNSFW = isNSFW;
            ModURL = modUrl;
            ModID = modID;
            ImageURL = imageURL;
        }

        public string ModName { get; }
        public string ModDescription { get; }
        public string ModAuthor { get; }
        public bool IsNSFW { get; }
        public string ModURL { get; }
        public string ModID { get; }
        public BitmapImage Image { get; set; }
        public string ImageURL { get; }
        
    }

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
        public BitmapImage WabbajackLogo { get; } = UIUtils.BitmapImageFromResource("Wabbajack.UI.banner.png");

        private bool _ShowNSFW;
        public bool ShowNSFW { get => _ShowNSFW; set => this.RaiseAndSetIfChanged(ref _ShowNSFW, value); }

        private bool _GCAfterUpdating = true;
        public bool GCAfterUpdating { get => _GCAfterUpdating; set => this.RaiseAndSetIfChanged(ref _GCAfterUpdating, value); }

        private bool _EnableSlideShow = true;
        public bool EnableSlideShow { get => _EnableSlideShow; set => this.RaiseAndSetIfChanged(ref _EnableSlideShow, value); }

        private BitmapImage _SplashScreenImage;
        public BitmapImage SplashScreenImage { get => _SplashScreenImage; set => this.RaiseAndSetIfChanged(ref _SplashScreenImage, value); }

        private string _SplashScreenModName = "Wabbajack";
        public string SplashScreenModName { get => _SplashScreenModName; set => this.RaiseAndSetIfChanged(ref _SplashScreenModName, value); }

        private string _SplashScreenAuthorName = "Halgari & the Wabbajack Team";
        public string SplashScreenAuthorName { get => _SplashScreenAuthorName; set => this.RaiseAndSetIfChanged(ref _SplashScreenAuthorName, value); }

        private string _SplashScreenSummary;
        public string SplashScreenSummary { get => _SplashScreenSummary; set => this.RaiseAndSetIfChanged(ref _SplashScreenSummary, value); }

        public IReactiveCommand SlideShowNextItemCommand { get; } = ReactiveCommand.Create(() => { });

        public SlideShow(AppState appState)
        {
            SlideShowElements = NexusApiClient.CachedSlideShow.ToList();
            CachedSlides = new Dictionary<string, Slide>();
            SlidesQueue = new Queue<Slide>();
            _random = new Random();
            AppState = appState;

            // Apply modlist properties when it changes
            this.WhenAny(x => x.AppState.ModList)
                .NotNull()
                .Subscribe(modList =>
                {
                    this.SplashScreenModName = modList.Name;
                    this.SplashScreenAuthorName = modList.Author;
                    this.SplashScreenSummary = modList.Description;
                })
                .DisposeWith(this.CompositeDisposable);

            // Update splashscreen when modlist changes
            Observable.CombineLatest(
                    this.WhenAny(x => x.AppState.ModList),
                    this.WhenAny(x => x.AppState.ModListPath),
                    this.WhenAny(x => x.EnableSlideShow),
                    (modList, modListPath, enableSlideShow) => (modList, modListPath, enableSlideShow))
                // Do any potential unzipping on a background thread
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(u =>
                {
                    if (u.enableSlideShow
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
                .Subscribe(bitmap => this.SplashScreenImage = bitmap)
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
                        this.WhenAny(x => x.EnableSlideShow),
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
                this.SplashScreenImage = AppState._noneImage;
                if (slide.ImageURL != null && slide.Image != null)
                {
                    if (!CachedSlides.ContainsKey(slide.ModID)) return;
                    this.SplashScreenImage = slide.Image;
                }

                this.SplashScreenModName = slide.ModName;
                this.SplashScreenAuthorName = slide.ModAuthor;
                this.SplashScreenSummary = slide.ModDescription;
                AppState._nexusSiteURL = slide.ModURL;
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

            if (checkLast)
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