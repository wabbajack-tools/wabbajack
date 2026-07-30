using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for ModListTileView.axaml
/// </summary>
public partial class ModListTileView : ReactiveUserControl<BaseModListMetadataVM>
{
    public ModListTileView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            // WPF: BindToStrict(this, v => v.ModlistImage.ImageSource) with a
            // System.Windows.Media.Imaging.BitmapImage.
            // BLOCKED/TODO: BaseModListMetadataVM.Image is still typed as
            // System.Windows.Media.Imaging.BitmapImage (WPF-only; see
            // Wabbajack.App.Wpf/ViewModels/Gallery/BaseModListMetadataVM.cs and
            // Wabbajack.App.Wpf/Util/UIUtils.cs DownloadBitmapImage). PORT_PLAN.md flags this as
            // a Phase-1 "Images" abstraction that has not landed yet (VM should expose a
            // stream/bytes producer that each head decodes to its own bitmap type). Until that
            // lands, ToAvaloniaBitmap below is a best-effort bridge that reads the WPF
            // BitmapImage's retained StreamSource (see UIUtils.BitmapImageFromStream, which sets
            // CacheOption=OnLoad and resets StreamSource.Position=0) into an Avalonia Bitmap. This
            // requires the Avalonia head to be able to resolve
            // System.Windows.Media.Imaging.BitmapImage at compile time, which in turn requires a
            // WPF-capable reference - it will not build as a pure Avalonia-only head until the
            // Core image abstraction replaces this property's type.
            ViewModel.WhenAnyValue(vm => vm.Image)
                     .Select(ToAvaloniaBitmap)
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

    private static Bitmap? ToAvaloniaBitmap(System.Windows.Media.Imaging.BitmapImage? source)
    {
        if (source?.StreamSource is null) return null;
        try
        {
            var stream = source.StreamSource;
            if (stream.CanSeek) stream.Position = 0;
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
