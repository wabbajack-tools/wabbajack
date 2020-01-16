using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack
{
    public class ModListGalleryVM : BackNavigatingVM
    {
        public MainWindowVM MWVM { get; }

        public ObservableCollectionExtended<ModListMetadataVM> ModLists { get; } = new ObservableCollectionExtended<ModListMetadataVM>();

        private int missingHashFallbackCounter;

        public ModListGalleryVM(MainWindowVM mainWindowVM)
            : base(mainWindowVM)
        {
            MWVM = mainWindowVM;

            Observable.Return(Unit.Default)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .SelectTask(async _ =>
                {
                    return (await ModlistMetadata.LoadFromGithub())
                        .AsObservableChangeSet(x => x.DownloadMetadata?.Hash ?? $"Fallback{missingHashFallbackCounter++}");
                })
                // Unsubscribe and release when not active
                .FlowSwitch(
                    this.WhenAny(x => x.IsActive), 
                    valueWhenOff: Observable.Return(ChangeSet<ModlistMetadata, string>.Empty))
                // Convert to VM and bind to resulting list
                .Switch()
                .ObserveOnGuiThread()
                .Transform(m => new ModListMetadataVM(this, m))
                .DisposeMany()
                .Bind(ModLists)
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            // Extra GC when navigating away, just to immediately clean up modlist metadata
            this.WhenAny(x => x.IsActive)
                .Where(x => !x)
                .Skip(1)
                .Delay(TimeSpan.FromMilliseconds(50))
                .Subscribe(_ =>
                {
                    GC.Collect();
                })
                .DisposeWith(CompositeDisposable);
        }
    }
}
