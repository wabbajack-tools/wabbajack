using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Controls;
using ReactiveUI;
using System.Windows;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for InstallationView.xaml
    /// </summary>
    public partial class InstallationView : ReactiveUserControl<InstallerVM>
    {
        public InstallationView()
        {
            InitializeComponent();
            this.WhenActivated(disposables =>
            {
                MidInstallDisplayGrid.Visibility = Visibility.Collapsed;
                LogView.Visibility = Visibility.Collapsed;
                CpuView.Visibility = Visibility.Collapsed;

                //ViewModel.WhenAnyValue(vm => vm.ModList.Name)
                //    .BindToStrict(this, view => view.Name)

                ViewModel.WhenAnyValue(vm => vm.BackCommand)
                    .BindToStrict(this, view => view.BackButton.Command)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.OpenReadmeCommand)
                    .BindToStrict(this, view => view.OpenReadmePreInstallButton.Command)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.VisitModListWebsiteCommand)
                    .BindToStrict(this, view => view.OpenWebsite.Command)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.LoadingLock.IsLoading)
                    .Select(loading => loading ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, view => view.ModlistLoadingRing.Visibility)
                    .DisposeWith(disposables);
                
                // Slideshow
                ViewModel.WhenAnyValue(vm => vm.SlideShowTitle)
                    .Select(f => f)
                    .BindToStrict(this, view => view.DetailImage.Title)
                    .DisposeWith(disposables);
                ViewModel.WhenAnyValue(vm => vm.SlideShowAuthor)
                    .BindToStrict(this, view => view.DetailImage.Author)
                    .DisposeWith(disposables);
                ViewModel.WhenAnyValue(vm => vm.SlideShowDescription)
                    .BindToStrict(this, view => view.DetailImage.Description)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.SlideShowImage)
                    .BindToStrict(this, view => view.DetailImage.Image)
                    .DisposeWith(disposables);

            });
        }
    }
}
