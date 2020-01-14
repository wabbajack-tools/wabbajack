using ReactiveUI;
using System;
using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
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
            ModName = NexusApiUtils.FixupSummary(m.ModName);
            ModID = m.ModID;
            ModDescription = NexusApiUtils.FixupSummary(m.Summary);
            ModAuthor = NexusApiUtils.FixupSummary(m.Author);
            IsNSFW = m.Adult;
            ModURL = m.NexusURL;
            ImageURL = m.SlideShowPic;
            ImageObservable = Observable.Return(ImageURL)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .DownloadBitmapImage((ex) => Utils.Log($"Skipping slide for mod {ModName} ({ModID})"))
                .Replay(1)
                .RefCount(TimeSpan.FromMilliseconds(5000));
        }
    }
}
