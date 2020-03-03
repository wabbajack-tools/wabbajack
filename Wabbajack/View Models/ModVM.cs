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
        public AbstractMetaState State { get; }

        // Image isn't exposed as a direct property, but as an observable.
        // This acts as a caching mechanism, as interested parties will trigger it to be created,
        // and the cached image will automatically be released when the last interested party is gone.
        public IObservable<BitmapImage> ImageObservable { get; }

        public ModVM(AbstractMetaState state)
        {
            State = state;

            ImageObservable = Observable.Return(State.ImageURL)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .DownloadBitmapImage((ex) => Utils.Log($"Skipping slide for mod {State.Name}"))
                .Replay(1)
                .RefCount(TimeSpan.FromMilliseconds(5000));
        }
    }
}
