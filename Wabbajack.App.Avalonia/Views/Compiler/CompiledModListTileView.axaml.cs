using Wabbajack.Paths.IO;
using Avalonia;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.Paths;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CompiledModListTileView.axaml
/// </summary>
public partial class CompiledModListTileView : ReactiveUserControl<CompiledModListTileVM>
{
    private Avalonia.Media.ImageBrush ModlistImage => (Avalonia.Media.ImageBrush)ModlistImageBorder.Background!;
    public CompiledModListTileView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            // WPF: UIUtils.TryGetBitmapImageFromFile returns a System.Windows.Media.Imaging.BitmapImage,
            // which is WPF-specific. Reproduced here with an Avalonia-compatible loader
            // (TryGetBitmapFromFile below) that returns Avalonia.Media.Imaging.Bitmap. BindToStrict
            // requires the source observable's value type to match the bound property's type exactly,
            // and ImageBrush.Source is typed as the broader Avalonia.Media.IImage, so the Bitmap is
            // upcast to IImage via Select before binding.
            ViewModel.WhenAnyValue(vm => vm.CompilerSettings.ModListImage)
                     .Select(TryGetBitmapFromFile)
                     .Select(bitmap => (Avalonia.Media.IImageBrushSource?)bitmap)
                     .BindToStrict(this, v => v.ModlistImage.Source)
                     .DisposeWith(dispose);

            // WPF: CompiledModListTile.Events().MouseDown -> Avalonia: PointerPressed on the root Grid.
            Observable.FromEventPattern<PointerPressedEventArgs>(
                          h => CompiledModListTile.PointerPressed += h,
                          h => CompiledModListTile.PointerPressed -= h)
                      .Select(_ => Unit.Default)
                      .InvokeCommand(this, x => x.ViewModel.CompileModListCommand)
                      .DisposeWith(dispose);

            // WPF: Visibility.Visible/Collapsed via a Select converter -> Avalonia: bind IsVisible
            // directly to the bool.
            ViewModel.WhenAnyValue(x => x.LoadingImageLock.IsLoading)
                      .BindToStrict(this, x => x.LoadingProgress.IsVisible)
                      .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.DeleteModListCommand, v => v.DeleteButton)
                .DisposeWith(dispose);

            // Faithful reproduction of the WPF MultiDataTrigger shared by the StackPanel Background,
            // the image Border's BorderBrush, both glow Rectangles' Opacity, the version Label's
            // Opacity, and both TextBlocks' Foreground:
            //   IsMouseOver(CompiledModListTile) == true AND IsMouseOver(DeleteButton) == false
            // Avalonia has no cross-element "hovered but not that other element" Style selector, so
            // this is reproduced with a combined IsPointerOver observable over both elements.
            var tileHover = CompiledModListTile.GetObservable(InputElement.IsPointerOverProperty);
            var deleteHover = DeleteButton.GetObservable(InputElement.IsPointerOverProperty);

            Observable.CombineLatest(tileHover, deleteHover, (tile, del) => tile && !del)
                      .DistinctUntilChanged()
                      .ObserveOnGuiThread()
                      .Subscribe(isHighlighted =>
                      {
                          HoverStackPanel.Background = isHighlighted
                              ? (IBrush)this.FindResource("MouseOverButtonBackground")
                              : (IBrush)this.FindResource("ComplementaryPrimary08Brush");

                          ImageBorder.BorderBrush = isHighlighted
                              ? (IBrush)this.FindResource("BorderInterestBrush")
                              : (IBrush)this.FindResource("ButtonBorder");

                          // Opacity changes animate via the DoubleTransition declared on each element
                          // in the .axaml, mirroring the WPF Storyboard's 0.08s DoubleAnimation.
                          TopGlowRectangle.Opacity = isHighlighted ? 0.2 : 0.15;
                          BottomGlowRectangle.Opacity = isHighlighted ? 0.2 : 0.15;
                          VersionLabel.Opacity = isHighlighted ? 1 : 0;

                          if (isHighlighted)
                          {
                              var primaryBrush = (IBrush)this.FindResource("PrimaryBrush");
                              ModListNameText.Foreground = primaryBrush;
                              ModListSourceText.Foreground = primaryBrush;
                          }
                          else
                          {
                              ModListNameText.ClearValue(TextBlock.ForegroundProperty);
                              ModListSourceText.ClearValue(TextBlock.ForegroundProperty);
                          }
                      })
                      .DisposeWith(dispose);
        });
    }

    private static Bitmap? TryGetBitmapFromFile(AbsolutePath path)
    {
        try
        {
            if (!path.FileExists()) return null;
            return new Bitmap(path.ToString());
        }
        catch (Exception)
        {
            return null;
        }
    }
}
