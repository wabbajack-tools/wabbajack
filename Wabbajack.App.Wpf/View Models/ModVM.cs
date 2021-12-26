using ReactiveUI;
using System;
using System.Reactive.Linq;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack
{
    public class ModVM : ViewModel
    {
        public IMetaState State { get; }

        // Image isn't exposed as a direct property, but as an observable.
        // This acts as a caching mechanism, as interested parties will trigger it to be created,
        // and the cached image will automatically be released when the last interested party is gone.
        public IObservable<BitmapImage> ImageObservable { get; }

        public ModVM(IMetaState state)
        {
            State = state;

            ImageObservable = Observable.Return(State.ImageURL.ToString())
                .ObserveOn(RxApp.TaskpoolScheduler)
                .DownloadBitmapImage((ex) => Utils.Log($"Skipping slide for mod {State.Name}"))
                .Replay(1)
                .RefCount(TimeSpan.FromMilliseconds(5000));
        }
    }
}
