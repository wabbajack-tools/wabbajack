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
                //MidInstallDisplayGrid.Visibility = Visibility.Collapsed;
                //LogView.Visibility = Visibility.Collapsed;
                //CpuView.Visibility = Visibility.Collapsed;

                ViewModel.WhenAnyValue(vm => vm.InstallState)
                    .Select(v => v != InstallState.Configuration ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, view => view.MidInstallDisplayGrid.Visibility)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(vm => vm.InstallState)
                    .Select(v => v == InstallState.Configuration ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, view => view.BottomButtonInputGrid.Visibility)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.InstallState)
                    .Select(es => es is InstallState.Success or InstallState.Failure ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, view => view.InstallComplete.Visibility)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.BackCommand)
                    .BindToStrict(this, view => view.BackButton.Command)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.OpenReadmeCommand)
                    .BindToStrict(this, view => view.OpenReadmePreInstallButton.Command)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(vm => vm.OpenDiscordButton)
                    .BindToStrict(this, view => view.OpenDiscordPreInstallButton.Command)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.VisitModListWebsiteCommand)
                    .BindToStrict(this, view => view.OpenWebsite.Command)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(vm => vm.VisitModListWebsiteCommand)
                    .BindToStrict(this, view => view.VisitWebsitePreInstallButton.Command)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(vm => vm.ShowManifestCommand)
                    .BindToStrict(this, view => view.ShowManifestPreInstallButton.Command)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.LoadingLock.IsLoading)
                    .Select(loading => loading ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, view => view.ModlistLoadingRing.Visibility)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(vm => vm.BeginCommand)
                    .BindToStrict(this, view => view.InstallationConfigurationView.BeginButton.Command)
                    .DisposeWith(disposables);
                
                // Status
                ViewModel.WhenAnyValue(vm => vm.StatusText)
                    .BindToStrict(this, view => view.TopProgressBar.Title)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.StatusProgress)
                    .Select(p => p.Value)
                    .BindToStrict(this, view => view.TopProgressBar.ProgressPercent)
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
