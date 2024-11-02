using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
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
            
            this.WhenAny(x => x.ViewModel.LoadingLock.ErrorState)
                .Select(e => (e?.Succeeded ?? true) ? Collapsed : Visible)
                .StartWith(Collapsed)
                .BindToStrict(this, x => x.ErrorIcon.Visibility)
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

            this.HasTagsFilter.Events().SelectedItemsChanged
                .Subscribe(args =>
                {
                    ViewModel.HasTags = new(HasTagsFilter.SelectedItems.Cast<ModListTag>());
                })
                .DisposeWith(dispose);
            
            /*
            this.IncludesTagsFilter.Events().SelectionChanged
                .Subscribe(args =>
                {
                    ViewModel.IncludedTags.AddRange(args.AddedItems.Cast<ModListGalleryVM.Tag>());
                    foreach(var tag in args.RemovedItems.Cast<ModListGalleryVM.Tag>())
                        ViewModel.IncludedTags.Remove(tag);
                })
                .DisposeWith(dispose);
                */
        });
    }
}
