using Avalonia;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for ModListTileView.axaml
/// </summary>
public partial class ModListTileView : ReactiveUserControl<BaseModListMetadataVM>
{
    private Avalonia.Media.ImageBrush ModlistImage => (Avalonia.Media.ImageBrush)ImageEffectBorder.Background!;
    public ModListTileView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            // WPF: BindToStrict(this, v => v.ModlistImage.ImageSource) with a
            // System.Windows.Media.Imaging.BitmapImage. BaseModListMetadataVM.Image is now typed
            // as Avalonia.Media.Imaging.Bitmap (see
            // Wabbajack.App.Avalonia/ViewModels/Gallery/BaseModListMetadataVM.cs), so it can be
            // bound directly to the ImageBrush's Source without any bridging conversion.
            ViewModel.WhenAnyValue(vm => vm.Image)
                     .Select(b => (Avalonia.Media.IImageBrushSource?)b)
                     .BindToStrict(this, v => v.ModlistImage.Source)
                     .DisposeWith(disposables);

            // Computed in the original WPF code-behind but never bound to anything there either
            // (dead code in the source file) - preserved verbatim for fidelity.
            var textXformed = ViewModel.WhenAnyValue(vm => vm.Metadata.Title)
                .CombineLatest(ViewModel.WhenAnyValue(vm => vm.Metadata.ImageContainsTitle),
                            ViewModel.WhenAnyValue(vm => vm.IsBroken))
                .Select(x => x.Second && !x.Third ? "" : x.First);

            // WPF: Visibility.Visible/Collapsed via a Select converter -> Avalonia: bind IsVisible
            // directly to the bool.
            ViewModel.WhenAnyValue(x => x.LoadingImageLock.IsLoading)
                .BindToStrict(this, x => x.LoadingProgress.IsVisible)
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

            // WPF: Grid.Visibility was a MultiBinding+MathConverter expression
            // "Or(!x,y) ? `Visible` : `Collapsed`" over (ImageContainsTitle, ModListTile.IsMouseOver).
            // MathConverter has no Avalonia build, so the compound condition
            // (!ImageContainsTitle || ModListTile.IsPointerOver) is reproduced here directly.
            var tileHover = ModListTile.GetObservable(InputElement.IsPointerOverProperty);
            ViewModel.WhenAnyValue(vm => vm.ImageContainsTitle)
                     .Select(x => !x)
                     .CombineLatest(tileHover, (noTitle, hover) => noTitle || hover)
                     .DistinctUntilChanged()
                     .BindToStrict(this, v => v.TitleOverlayGrid.IsVisible)
                     .DisposeWith(disposables);

            // Faithful reproduction of the WPF MultiDataTrigger shared by ImageEffectBorder's Effect
            // and ImageEffectGrid's Background:
            //   IsMouseOver(ModListTile) == true AND IsBroken == true
            // Avalonia has no bound-property + pseudo-class combined Style selector, so this is
            // reproduced with a combined IsPointerOver/IsBroken observable, mirroring the
            // CompiledModListTileView.axaml.cs precedent for cross-element/VM hover triggers.
            var isBroken = ViewModel.WhenAnyValue(vm => vm.IsBroken);
            Observable.CombineLatest(tileHover, isBroken, (hover, broken) => hover && broken)
                      .DistinctUntilChanged()
                      .ObserveOnGuiThread()
                      .Subscribe(isHighlightedBroken =>
                      {
                          ImageEffectBorder.Effect = isHighlightedBroken
                              ? new BlurEffect { Radius = 25 }
                              : null;

                          ImageEffectGrid.Background = isHighlightedBroken
                              ? (IBrush)this.FindResource("UnavailableDarkBrush")!
                              : null;
                      })
                      .DisposeWith(disposables);

            // WPF DataTrigger on ModListTile.IsMouseOver animated TopGlowRectangle's Opacity
            // (0.05 <-> 0.3) via a 0.08s Storyboard/DoubleAnimation. The Transition declared on the
            // Rectangle in the .axaml reproduces the animation; this subscription drives the target
            // value, mirroring the CompiledModListTileView.axaml.cs precedent.
            tileHover
                .DistinctUntilChanged()
                .Select(hover => hover ? 0.3 : 0.05)
                .BindToStrict(this, v => v.TopGlowRectangle.Opacity)
                .DisposeWith(disposables);

            // WPF MultiDataTrigger on ModListTile.IsMouseOver animated MidGlowRectangle's Opacity
            // (0 <-> 0.75) via a 0.08s Storyboard/DoubleAnimation, same pattern as above.
            tileHover
                .DistinctUntilChanged()
                .Select(hover => hover ? 0.75 : 0.0)
                .BindToStrict(this, v => v.MidGlowRectangle.Opacity)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.DetailsCommand, v => v.ModlistButton)
                .DisposeWith(disposables);
        });
    }
}
