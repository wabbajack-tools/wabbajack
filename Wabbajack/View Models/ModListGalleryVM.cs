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
    public class ModListGalleryVM : ViewModel
    {
        public MainWindowVM MWVM { get; }

        public ObservableCollectionExtended<ModListMetadataVM> ModLists { get; } = new ObservableCollectionExtended<ModListMetadataVM>();

        public IReactiveCommand BackCommand { get; }
        public IReactiveCommand RefreshCommand { get; }

        private int missingHashFallbackCounter;

        public ModListGalleryVM(MainWindowVM mainWindowVM)
        {
            MWVM = mainWindowVM;
            BackCommand = ReactiveCommand.Create(
                execute: () => mainWindowVM.ActivePane = mainWindowVM.ModeSelectionVM);
            RefreshCommand = ReactiveCommand.Create(() => { });

            RefreshCommand.StartingExecution()
                .StartWith(Unit.Default)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(_ =>
                {
                    return ModlistMetadata.LoadFromGithub()
                        .AsObservableChangeSet(x => x.DownloadMetadata?.Hash ?? $"Fallback{missingHashFallbackCounter++}");
                })
                .Switch()
                .ObserveOnGuiThread()
                .Transform(m => new ModListMetadataVM(this, m))
                .Bind(ModLists)
                .Subscribe()
                .DisposeWith(CompositeDisposable);
        }
    }
}
