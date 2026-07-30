using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using ReactiveMarbles.ObservableEvents;
using ReactiveUI;

namespace Wabbajack;

public partial class ModListGalleryView : ReactiveUserControl<ModListGalleryVM>
{
    public ModListGalleryView()
    {
        InitializeComponent();

        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel.IsResolvingProtocol)
                .Select(isBusy => !isBusy)
                .BindToStrict(this, x => x.ModListGalleryControl.IsEnabled)
                .DisposeWith(dispose);

            // WPF's Visibility (Visible/Collapsed) maps to Avalonia's bool IsVisible, so the
            // Visible/Collapsed projection is no longer needed - the bool binds directly.
            this.WhenAnyValue(x => x.ViewModel.IsResolvingProtocol)
                .BindToStrict(this, x => x.ProtocolOverlay.IsVisible)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.ProtocolStatusText)
                .BindToStrict(this, x => x.ProtocolOverlayText.Text)
                .DisposeWith(dispose);

            this.WhenAny(x => x.ViewModel.ModLists)
                .BindToStrict(this, x => x.ModListGalleryControl.ItemsSource)
                .DisposeWith(dispose);



            this.WhenAny(x => x.ViewModel.SmallestSizedModlist)
                .Where(x => x != null)
                .Select(x => x.Metadata.DownloadMetadata.TotalSize / Math.Pow(1024, 3))
                .BindToStrict(this, x => x.SizeSliderFilter.Minimum)
                .DisposeWith(dispose);

            this.WhenAny(x => x.ViewModel.LargestSizedModlist)
                .Where(x => x != null)
                .Select(x => x.Metadata.DownloadMetadata.TotalSize / Math.Pow(1024, 3))
                .BindToStrict(this, x => x.SizeSliderFilter.Maximum)
                .DisposeWith(dispose);

            this.WhenAny(x => x.ViewModel.LoadingLock.IsLoading)
                .StartWith(false)
                .BindTo(this, x => x.LoadingRing.IsVisible)
                .DisposeWith(dispose);

            this.WhenAny(x => x.ViewModel.ModLists.Count)
                .CombineLatest(this.WhenAnyValue(x => x.ViewModel.LoadingLock.IsLoading))
                .Select(x => x.First == 0 && !x.Second)
                .DistinctUntilChanged()
                .StartWith(false)
                .BindToStrict(this, x => x.NoneFound.IsVisible)
                .DisposeWith(dispose);

            this.BindStrict(ViewModel, vm => vm.Search, x => x.SearchBox.Text)
                .DisposeWith(dispose);
            this.BindStrict(ViewModel, vm => vm.OnlyInstalled, x => x.OnlyInstalledCheckbox.IsChecked)
                .DisposeWith(dispose);
            this.BindStrict(ViewModel, vm => vm.IncludeNSFW, x => x.IncludeNSFW.IsChecked)
                .DisposeWith(dispose);
            this.BindStrict(ViewModel, vm => vm.IncludeUnofficial, x => x.IncludeUnofficial.IsChecked)
                .DisposeWith(dispose);
            this.BindStrict(ViewModel, vm => vm.ExcludeMods, x => x.ExcludeModsCheckbox.IsChecked)
                .DisposeWith(dispose);

            this.BindStrict(ViewModel,
                    vm => vm.MinModlistSize,
                    view => view.SizeSliderFilter.LowerValue,
                    vmProp => vmProp / Math.Pow(1024, 3),
                    vProp => vProp * Math.Pow(1024, 3))
                .DisposeWith(dispose);

            this.BindStrict(ViewModel,
                    vm => vm.MaxModlistSize,
                    view => view.SizeSliderFilter.UpperValue,
                    vmProp => vmProp / Math.Pow(1024, 3),
                    vProp => vProp * Math.Pow(1024, 3))
                .DisposeWith(dispose);

            // TODO(avalonia-bind): local:MultiSelectComboBox is a single non-generic control shared by
            // both HasModsFilter (ModListMod items) and HasTagsFilter (ModListTag items). A strict
            // two-way BindStrict here requires SelectedItems' declared type to exactly equal both
            // ObservableCollection<ModListMod> and ObservableCollection<ModListTag> at once, which is
            // impossible for a single property - so this can't be made to compile confidently from this
            // file alone. View -> ViewModel sync is already handled below via the SelectedItemsChanged
            // subscriptions; only the ViewModel -> View direction (e.g. clearing selection on
            // ResetFiltersCommand) is lost until MultiSelectComboBox's SelectedItems type is finalized.
            // this.BindStrict(ViewModel,
            //     vm => vm.HasMods,
            //     v => v.HasModsFilter.SelectedItems)
            //     .DisposeWith(dispose);
            //
            // this.BindStrict(ViewModel,
            //     vm => vm.HasTags,
            //     v => v.HasTagsFilter.SelectedItems)
            //     .DisposeWith(dispose);

            // Selector return values are explicitly cast to IEnumerable (rather than left as
            // ObservableCollection<T>) so the inferred TOut matches ItemsSource's declared type -
            // without the cast, the view-property and selector expressions infer conflicting exact
            // types and the call fails to compile.
            this.OneWayBindStrict(ViewModel,
                vm => vm.AllMods,
                v => v.HasModsFilter.ItemsSource,
                mods => (IEnumerable)new ObservableCollection<ModListMod>(mods))
                .DisposeWith(dispose);

            this.OneWayBindStrict(ViewModel,
                vm => vm.AllTags,
                v => v.HasTagsFilter.ItemsSource,
                tags => (IEnumerable)new ObservableCollection<ModListTag>(tags))
                .DisposeWith(dispose);

            HasTagsFilter.Events().SelectedItemsChanged
                .Subscribe(_ =>
                {
                    ViewModel.HasTags = new ObservableCollection<ModListTag>(HasTagsFilter.SelectedItems.Cast<ModListTag>());
                })
                .DisposeWith(dispose);

            HasModsFilter.Events().SelectedItemsChanged
                .Subscribe(_ =>
                {
                    ViewModel.HasMods = new ObservableCollection<ModListMod>(HasModsFilter.SelectedItems.Cast<ModListMod>());
                })
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, x => x.ResetFiltersCommand, x => x.ResetFiltersButton)
                .DisposeWith(dispose);
        });
    }
}
