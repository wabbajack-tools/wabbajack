using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for CreateModListTileView.xaml
    /// </summary>
    public partial class CreatedModListTileView : ReactiveUserControl<CreatedModlistVM>
    {
        public CreatedModListTileView()
        {
            InitializeComponent();
            this.WhenActivated(disposables =>
            {
                ViewModel.WhenAnyValue(vm => vm.CompilerSettings.ModListImage)
                         .Select(imagePath => { UIUtils.TryGetBitmapImageFromFile(imagePath, out var bitmapImage); return bitmapImage; })
                         .BindToStrict(this, v => v.ModlistImage.ImageSource)
                         .DisposeWith(disposables);


                /*
                ViewModel.WhenAnyValue(x => x.LoadingImageLock.IsLoading)
                    .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, x => x.LoadingProgress.Visibility)
                    .DisposeWith(disposables);
                */
            });
        }
    }
}
