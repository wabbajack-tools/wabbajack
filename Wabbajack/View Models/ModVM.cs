using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack
{
    public class ModVM : ViewModel
    {
        public string ModName { get; }

        public string ModID { get; }

        public string ModDescription { get; }

        public string ModAuthor { get; }

        public bool IsNSFW { get; }

        public string ModURL { get; }
        
        public string ImageURL { get; }

        // Image isn't exposed as a direct property, but as an observable.
        // This acts as a caching mechanism, as interested parties will trigger it to be created,
        // and the cached image will automatically be released when the last interested party is gone.
        public IObservable<BitmapImage> ImageObservable { get; }

        public ModVM(NexusDownloader.State m)
        {
            this.ModName = NexusApiUtils.FixupSummary(m.ModName);
            this.ModID = m.ModID;
            this.ModDescription = NexusApiUtils.FixupSummary(m.Summary);
            this.ModAuthor = NexusApiUtils.FixupSummary(m.Author);
            this.IsNSFW = m.Adult;
            this.ModURL = m.NexusURL;
            this.ImageURL = m.SlideShowPic;
            this.ImageObservable = Observable.Return(this.ImageURL)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .SelectTask(async url =>
                {
                    try
                    {
                        var ret = new MemoryStream();
                        using (Stream stream = await new HttpClient().GetStreamAsync(url))
                        {
                            stream.CopyTo(ret);
                        }

                        ret.Seek(0, SeekOrigin.Begin);
                        return ret;
                    }
                    catch (Exception ex)
                    {
                        Utils.LogToFile($"Exception while caching slide {this.ModName} ({this.ModID})\n{ex.ExceptionToString()}");
                        return default(MemoryStream);
                    }
                })
                .ObserveOn(RxApp.MainThreadScheduler)
                .Select(memStream =>
                {
                    if (memStream == null) return default(BitmapImage);
                    try
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = memStream;
                        image.EndInit();
                        image.Freeze();
                        return image;
                    }
                    catch (Exception ex)
                    {
                        Utils.LogToFile($"Exception while caching slide {this.ModName} ({this.ModID})\n{ex.ExceptionToString()}");
                        return default(BitmapImage);
                    }
                    finally
                    {
                        memStream?.Dispose();
                    }
                })
                .Replay(1)
                .RefCount();
        }
    }
}
