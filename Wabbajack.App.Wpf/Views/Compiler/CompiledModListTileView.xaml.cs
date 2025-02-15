using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;
using ReactiveMarbles.ObservableEvents;
using System.Reactive;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CreateModListTileView.xaml
/// </summary>
public partial class CompiledModListTileView : ReactiveUserControl<CompiledModListTileVM>
{
    public CompiledModListTileView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            ViewModel.WhenAnyValue(vm => vm.CompilerSettings.ModListImage)
                     .Select(imagePath => { UIUtils.TryGetBitmapImageFromFile(imagePath, out var bitmapImage); return bitmapImage; })
                     .BindToStrict(this, v => v.ModlistImage.ImageSource)
                     .DisposeWith(dispose);

            CompiledModListTile
            .Events().MouseDown
            .Select(args => Unit.Default)
            .InvokeCommand(this, x => x.ViewModel.CompileModListCommand)
            .DisposeWith(dispose);


            ViewModel.WhenAnyValue(x => x.LoadingImageLock.IsLoading)
                .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                .BindToStrict(this, x => x.LoadingProgress.Visibility)
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.DeleteModListCommand, v => v.DeleteButton)
                .DisposeWith(dispose);
        });
    }
}
