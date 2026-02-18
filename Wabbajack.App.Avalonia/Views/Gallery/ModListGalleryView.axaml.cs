using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.App.Avalonia.ViewModels.Gallery;

namespace Wabbajack.App.Avalonia.Views.Gallery;

public partial class ModListGalleryView : ReactiveUserControl<ModListGalleryVM>
{
    public ModListGalleryView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            // Two-way: search text
            this.Bind(ViewModel, vm => vm.Search, v => v.SearchBox.Text)
                .DisposeWith(disposables);

            // One-way: populate game filter items; selected item is two-way
            this.OneWayBind(ViewModel, vm => vm.GameTypeEntries, v => v.GameCombo.ItemsSource)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedGameTypeEntry, v => v.GameCombo.SelectedItem)
                .DisposeWith(disposables);

            // Checkboxes
            this.Bind(ViewModel, vm => vm.IncludeUnofficial, v => v.IncludeUnofficial.IsChecked)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IncludeNSFW, v => v.IncludeNSFW.IsChecked)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.OnlyInstalled, v => v.OnlyInstalledCheckbox.IsChecked)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ExcludeMods, v => v.ExcludeModsCheckbox.IsChecked)
                .DisposeWith(disposables);

            // Size sliders — initialise bounds when VM sets them
            ViewModel!.WhenAnyValue(vm => vm.SmallestSizedModlist)
                .Where(v => v != null)
                .Subscribe(v =>
                {
                    MinSizeSlider.Minimum = v!.Metadata.DownloadMetadata.TotalSize;
                    MaxSizeSlider.Minimum = v.Metadata.DownloadMetadata.TotalSize;
                })
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.LargestSizedModlist)
                .Where(v => v != null)
                .Subscribe(v =>
                {
                    MinSizeSlider.Maximum = v!.Metadata.DownloadMetadata.TotalSize;
                    MaxSizeSlider.Maximum = v.Metadata.DownloadMetadata.TotalSize;
                })
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.MinModlistSize, v => v.MinSizeSlider.Value)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.MaxModlistSize, v => v.MaxSizeSlider.Value)
                .DisposeWith(disposables);

            // Size labels
            ViewModel.WhenAnyValue(vm => vm.MinModlistSize)
                .Subscribe(v => MinSizeLabel.Text = UIUtils.FormatBytes((long)v))
                .DisposeWith(disposables);
            ViewModel.WhenAnyValue(vm => vm.MaxModlistSize)
                .Subscribe(v => MaxSizeLabel.Text = UIUtils.FormatBytes((long)v))
                .DisposeWith(disposables);

            // Tag + mod ListBoxes
            this.OneWayBind(ViewModel, vm => vm.AllTags, v => v.HasTagsFilter.ItemsSource)
                .DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.AllMods, v => v.HasModsFilter.ItemsSource)
                .DisposeWith(disposables);

            // Gallery items
            this.OneWayBind(ViewModel, vm => vm.ModLists, v => v.GalleryControl.ItemsSource)
                .DisposeWith(disposables);

            // Loading bar
            this.OneWayBind(ViewModel, vm => vm.LoadingLock.IsLoading, v => v.LoadingBar.IsVisible)
                .DisposeWith(disposables);

            // Empty-state panel: visible when not loading and no results
            ViewModel.WhenAnyValue(vm => vm.LoadingLock.IsLoading)
                .CombineLatest(ViewModel.WhenAnyValue(vm => vm.ModLists.Count))
                .Subscribe(t => NoneFound.IsVisible = !t.First && t.Second == 0)
                .DisposeWith(disposables);

            // Reset filters button
            this.BindCommand(ViewModel, vm => vm.ResetFiltersCommand, v => v.ResetFiltersButton)
                .DisposeWith(disposables);
        });
    }
}
