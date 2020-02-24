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
        public IMetaState State { get; }

        public string URL { get; }
        public string Name { get; }
        public string Author { get; }
        public string Version { get; }
        public string ImageURL { get; }
        public bool IsNSFW { get; }
        public string Description { get; }

        // Image isn't exposed as a direct property, but as an observable.
        // This acts as a caching mechanism, as interested parties will trigger it to be created,
        // and the cached image will automatically be released when the last interested party is gone.
        public IObservable<BitmapImage> ImageObservable { get; }

        public ModVM(IMetaState state)
        {
            State = state;

            URL = state.URL;
            ImageURL = state.ImageURL;
            IsNSFW = state.IsNSFW;
            Version = state.Version;

            var isNexus = state.GetType() == typeof(NexusDownloader.State);
            
            Name = isNexus ? NexusApiUtils.FixupSummary(state.Name) : state.Name;
            Author = isNexus ? NexusApiUtils.FixupSummary(state.Author) : state.Author;
            Description = isNexus ? NexusApiUtils.FixupSummary(state.Description) : state.Description;

            ImageObservable = Observable.Return(ImageURL)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .DownloadBitmapImage((ex) => Utils.Log($"Skipping slide for mod {Name}"))
                .Replay(1)
                .RefCount(TimeSpan.FromMilliseconds(5000));
        }
    }
}
