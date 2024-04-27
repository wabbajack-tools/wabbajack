using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for CreateModListTileView.xaml
    /// </summary>
    public partial class CreateModListTileView : ReactiveUserControl<CreateModListMetadataVM>
    {
        public CreateModListTileView()
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
            });
        }
    }
}
