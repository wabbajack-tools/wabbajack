using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for ModListTileView.xaml
/// </summary>
public partial class ModListTileView : ReactiveUserControl<BaseModListMetadataVM>
{
    public ModListTileView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            ViewModel.WhenAnyValue(vm => vm.Image)
                     .BindToStrict(this, v => v.ModlistImage.ImageSource)
                     .DisposeWith(disposables);

            var textXformed = ViewModel.WhenAnyValue(vm => vm.Metadata.Title)
                .CombineLatest(ViewModel.WhenAnyValue(vm => vm.Metadata.ImageContainsTitle),
                            ViewModel.WhenAnyValue(vm => vm.IsBroken))
                .Select(x => x.Second && !x.Third ? "" : x.First);

            ViewModel.WhenAnyValue(x => x.LoadingImageLock.IsLoading)
                .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                .BindToStrict(this, x => x.LoadingProgress.Visibility)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(x => x.Metadata.DownloadMetadata.SizeOfArchives)
                     .Select(x => UIUtils.FormatBytes(x, round: true))
                     .BindToStrict(this, v => v.DownloadSizeRun.Text)
                     .DisposeWith(disposables);

            ViewModel.WhenAnyValue(x => x.Metadata.DownloadMetadata.SizeOfInstalledFiles)
                     .Select(x => UIUtils.FormatBytes(x, round: true))
                     .BindToStrict(this, v => v.InstallSizeRun.Text)
                     .DisposeWith(disposables);

            /*
            ViewModel.WhenAnyValue(x => x.Metadata.DownloadMetadata.TotalSize)
                     .Select(x => UIUtils.FormatBytes(x, round: true))
                     .BindToStrict(this, v => v.TotalSizeRun.Text)
                     .DisposeWith(disposables);
            */

            this.BindCommand(ViewModel, vm => vm.DetailsCommand, v => v.ModlistButton)
                .DisposeWith(disposables);
        });
    }
}
