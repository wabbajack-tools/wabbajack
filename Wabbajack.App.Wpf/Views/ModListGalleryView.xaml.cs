using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;

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
                .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                .StartWith(Visibility.Collapsed)
                .BindTo(this, x => x.LoadingRing.Visibility)
                .DisposeWith(dispose);
            
            this.WhenAny(x => x.ViewModel.LoadingLock.ErrorState)
                .Select(e => (e?.Succeeded ?? true) ? Visibility.Collapsed : Visibility.Visible)
                .StartWith(Visibility.Collapsed)
                .BindToStrict(this, x => x.ErrorIcon.Visibility)
                .DisposeWith(dispose);
            
            this.WhenAny(x => x.ViewModel.ModLists.Count)
                .CombineLatest(this.WhenAnyValue(x => x.ViewModel.LoadingLock.IsLoading))
                .Select(x => x.First == 0 && !x.Second)
                .DistinctUntilChanged()
                .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                .StartWith(Visibility.Collapsed)
                .BindToStrict(this, x => x.NoneFound.Visibility)
                .DisposeWith(dispose);

            this.BindStrict(ViewModel, vm => vm.Search, x => x.SearchBox.Text)
                .DisposeWith(dispose);
            this.BindStrict(ViewModel, vm => vm.OnlyInstalled, x => x.OnlyInstalledCheckbox.IsChecked)
                .DisposeWith(dispose);
            this.BindStrict(ViewModel, vm => vm.ShowNSFW, x => x.ShowNSFW.IsChecked)
                .DisposeWith(dispose);
            this.BindStrict(ViewModel, vm => vm.ShowUnofficialLists, x => x.ShowUnofficialLists.IsChecked)
                .DisposeWith(dispose);

            this.BindStrict(ViewModel,
                            vm => vm.MinModlistSize,
                            view => view.SizeSliderFilter.LowerValue,
                            (double vmProp) => vmProp / Math.Pow(1024, 3),
                            (double vProp) => vProp * Math.Pow(1024, 3))
                .DisposeWith(dispose);

            this.BindStrict(ViewModel,
                            vm => vm.MaxModlistSize,
                            view => view.SizeSliderFilter.UpperValue,
                            (double vmProp) => vmProp / Math.Pow(1024, 3),
                            (double vProp) => vProp * Math.Pow(1024, 3))
                .DisposeWith(dispose);

            this.WhenAny(x => x.ViewModel.ClearFiltersCommand)
                .BindToStrict(this, x => x.ClearFiltersButton.Command)
                .DisposeWith(dispose);
        });
    }
}
