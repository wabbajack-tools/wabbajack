using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveMarbles.ObservableEvents;
using ReactiveUI;
using static System.Windows.Visibility;

namespace Wabbajack;

public partial class ModListGalleryView : ReactiveUserControl<ModListGalleryVM>
{
    public ModListGalleryView()
    {
        InitializeComponent();

        this.WhenActivated(dispose =>
        {
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
                .Select(x => x ? Visible : Collapsed)
                .StartWith(Collapsed)
                .BindTo(this, x => x.LoadingRing.Visibility)
                .DisposeWith(dispose);
            
            this.WhenAny(x => x.ViewModel.ModLists.Count)
                .CombineLatest(this.WhenAnyValue(x => x.ViewModel.LoadingLock.IsLoading))
                .Select(x => x.First == 0 && !x.Second)
                .DistinctUntilChanged()
                .Select(x => x ? Visible : Collapsed)
                .StartWith(Collapsed)
                .BindToStrict(this, x => x.NoneFound.Visibility)
                .DisposeWith(dispose);

            this.BindStrict(ViewModel, vm => vm.Search, x => x.SearchBox.Text)
                .DisposeWith(dispose);
            this.BindStrict(ViewModel, vm => vm.OnlyInstalled, x => x.OnlyInstalledCheckbox.IsChecked)
                .DisposeWith(dispose);
            this.BindStrict(ViewModel, vm => vm.IncludeNSFW, x => x.IncludeNSFW.IsChecked)
                .DisposeWith(dispose);
            this.BindStrict(ViewModel, vm => vm.IncludeUnofficial, x => x.IncludeUnofficial.IsChecked)
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
            
            this.BindStrict(ViewModel,
                vm => vm.HasMods,
                v => v.HasModsFilter.SelectedItems)
                .DisposeWith(dispose);
            
            this.BindStrict(ViewModel,
                vm => vm.HasTags,
                v => v.HasTagsFilter.SelectedItems)
                .DisposeWith(dispose);

            this.OneWayBindStrict(ViewModel,
                vm => vm.AllMods,
                v => v.HasModsFilter.ItemsSource,
                mods => new ObservableCollection<ModListMod>(mods))
                .DisposeWith(dispose);
            
            this.OneWayBindStrict(ViewModel,
                vm => vm.AllTags,
                v => v.HasTagsFilter.ItemsSource,
                tags => new ObservableCollection<ModListTag>(tags))
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
