using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.Controls;
using Wabbajack.App.Avalonia.ViewModels.Gallery;

namespace Wabbajack.App.Avalonia.Views.Gallery;

public partial class ModListGalleryView : ReactiveUserControl<ModListGalleryVM>
{
    public ModListGalleryView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            // The size filter's ends: the smallest and largest list, in GB to one place, as WPF's MathConverter
            // "Round(x,1)" with "{0} GB" printed them.
            this.WhenAnyValue(x => x.ViewModel!.SmallestSizedModlist)
                .Where(x => x != null)
                .Subscribe(x => SizeMinText.Text = $"{Math.Round(x!.Metadata.DownloadMetadata!.TotalSize / Math.Pow(1024, 3), 1)} GB")
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.ViewModel!.LargestSizedModlist)
                .Where(x => x != null)
                .Subscribe(x => SizeMaxText.Text = $"{Math.Round(x!.Metadata.DownloadMetadata!.TotalSize / Math.Pow(1024, 3), 1)} GB")
                .DisposeWith(dispose);

            // The slider works in GB and the view model in bytes, as the WPF view's bindings converted them.
            const double gb = 1024.0 * 1024 * 1024;
            this.WhenAnyValue(x => x.ViewModel!.SmallestSizedModlist)
                .Where(x => x != null)
                .Subscribe(x => SizeSliderFilter.Minimum = x!.Metadata.DownloadMetadata!.TotalSize / gb)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.ViewModel!.LargestSizedModlist)
                .Where(x => x != null)
                .Subscribe(x => SizeSliderFilter.Maximum = x!.Metadata.DownloadMetadata!.TotalSize / gb)
                .DisposeWith(dispose);
            this.Bind(ViewModel, vm => vm.MinModlistSize, v => v.SizeSliderFilter.LowerValue, b => b / gb, g => g * gb)
                .DisposeWith(dispose);
            this.Bind(ViewModel, vm => vm.MaxModlistSize, v => v.SizeSliderFilter.UpperValue, b => b / gb, g => g * gb)
                .DisposeWith(dispose);

            // The mod and tag pickers. The view model's collection goes in one way; the control's changes come
            // back as a new collection, because the filter watches the property rather than its contents.
            this.OneWayBind(ViewModel, vm => vm.AllMods, v => v.HasModsFilter.ItemsSource,
                    mods => new ObservableCollection<ModListMod>(mods))
                .DisposeWith(dispose);
            this.OneWayBind(ViewModel, vm => vm.AllTags, v => v.HasTagsFilter.ItemsSource,
                    tags => new ObservableCollection<ModListTag>(tags))
                .DisposeWith(dispose);
            this.OneWayBind(ViewModel, vm => vm.HasMods, v => v.HasModsFilter.SelectedItems, mods => (IList)mods)
                .DisposeWith(dispose);
            this.OneWayBind(ViewModel, vm => vm.HasTags, v => v.HasTagsFilter.SelectedItems, tags => (IList)tags)
                .DisposeWith(dispose);
            Observable.FromEventPattern<MultiSelectComboBoxSelectionChangedEventArgs>(
                    h => HasModsFilter.SelectedItemsChanged += h, h => HasModsFilter.SelectedItemsChanged -= h)
                .Subscribe(_ => ViewModel!.HasMods =
                    new ObservableCollection<ModListMod>(HasModsFilter.SelectedItems!.Cast<ModListMod>()))
                .DisposeWith(dispose);
            Observable.FromEventPattern<MultiSelectComboBoxSelectionChangedEventArgs>(
                    h => HasTagsFilter.SelectedItemsChanged += h, h => HasTagsFilter.SelectedItemsChanged -= h)
                .Subscribe(_ => ViewModel!.HasTags =
                    new ObservableCollection<ModListTag>(HasTagsFilter.SelectedItems!.Cast<ModListTag>()))
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.LoadingLock.IsLoading)
                .Subscribe(loading =>
                {
                    LoadingRing.IsVisible = loading;
                    LoadingRing.IsActive = loading;
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel!.ModLists.Count)
                .CombineLatest(this.WhenAnyValue(x => x.ViewModel!.LoadingLock.IsLoading))
                .Select(x => x.First == 0 && !x.Second)
                .DistinctUntilChanged()
                .Subscribe(none => NoneFound.IsVisible = none)
                .DisposeWith(dispose);
        });
    }
}
