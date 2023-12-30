using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;

namespace Wabbajack
{
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

                this.WhenAny(x => x.ViewModel.MinSizeModlist)
                    .Where(x => x != null)
                    .Select(x => x.Metadata.DownloadMetadata.TotalSize / Math.Pow(1024, 3))
                    .BindToStrict(this, x => x.SizeSliderFilter.Minimum)
                    .DisposeWith(dispose);

                this.WhenAny(x => x.ViewModel.MaxSizeModlist)
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

                this.WhenAny(x => x.SizeSliderFilter.LowerValue)
                    .Select(x => x * Math.Pow(1024, 3))
                    .BindToStrict(ViewModel, vm => vm.MinSizeFilter)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.SizeSliderFilter.UpperValue)
                    .Select(x => x * Math.Pow(1024, 3))
                    .BindToStrict(ViewModel, vm => vm.MaxSizeFilter)
                    .DisposeWith(dispose);
                /*
                this.BindStrict(ViewModel, vm => vm.MinSizeFilter, x => x.SizeSliderFilter.LowerValue)
                    .DisposeWith(dispose);
                this.BindStrict(ViewModel, vm => vm.MaxSizeFilter, x => x.SizeSliderFilter.UpperValue)
                    .DisposeWith(dispose);
                */

                this.WhenAny(x => x.ViewModel.ClearFiltersCommand)
                    .BindToStrict(this, x => x.ClearFiltersButton.Command)
                    .DisposeWith(dispose);
            });
        }
    }
}
