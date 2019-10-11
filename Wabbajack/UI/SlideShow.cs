using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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

    internal class SlideShow
    {
        private readonly Random _random;
        private Slide _lastSlide;
        private const bool UseSync = false;
        private const int MaxCacheSize = 10;
        private readonly AppState _appState;

        public SlideShow(AppState appState, bool checkCache)
        {
            SlideShowElements = new List<Slide>();
            CachedSlides = new Dictionary<string, Slide>();
            SlidesQueue = new Queue<Slide>();
            _random = new Random();
            _appState = appState;

            if (!checkCache) return;
            IEnumerable<Slide> files = NexusApiClient.CachedSlideShow;
            IEnumerable<Slide> enumerable = files.ToList();
            if (enumerable.Any())
            {
                SlideShowElements = enumerable.ToList();
            }
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
            if (!_appState.EnableSlideShow || !_appState.installing || SlidesQueue.Count==0) return;
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
                if (AppState.GcCollect)
                    GC.Collect();
            }

            if (!slide.IsNSFW || (slide.IsNSFW && ShowNSFW))
            {
                _appState.SplashScreenImage = _appState._noneImage;
                if (slide.ImageURL != null && slide.Image != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (!CachedSlides.ContainsKey(slide.ModID)) return;
                        _appState.SplashScreenImage = slide.Image;
                    });
                }

                _appState.SplashScreenModName = slide.ModName;
                _appState.SplashScreenAuthorName = slide.ModAuthor;
                _appState.SplashScreenSummary = slide.ModDescription;
                _appState._nexusSiteURL = slide.ModURL;
            }

            _appState.lastSlideShowUpdate = DateTime.Now;

            SlidesQueue.Dequeue();
            QueueRandomSlide(false, true);
        }

        private void CacheImage(Slide slide)
        {
            using (var ms = new MemoryStream())
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

        public bool ShowNSFW { get; set; }
        public List<Slide> SlideShowElements { get; set; }

        public Dictionary<string, Slide> CachedSlides { get; }

        public Queue<Slide> SlidesQueue { get; }
    }
}