using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using System.Windows;
using Microsoft.Toolkit.HighPerformance;
using Humanizer;
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.Devices;
using System.Management;
using System.Text.RegularExpressions;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using System.Threading.Tasks;

namespace Wabbajack;

/// <summary>
/// Interaction logic for InstallationView.xaml
/// </summary>
public partial class InstallationView : ReactiveUserControl<InstallationVM>
{
    public InstallationView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            //MidInstallDisplayGrid.Visibility = Visibility.Collapsed;
            //LogView.Visibility = Visibility.Collapsed;
            //CpuView.Visibility = Visibility.Collapsed;

            this.Bind(ViewModel, vm => vm.Installer.Location, view => view.InstallationLocationPicker.PickerVM)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Installer.DownloadLocation, view => view.DownloadLocationPicker.PickerVM)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.SuggestedInstallFolder)
                     .ObserveOnGuiThread()
                     .Subscribe(x =>
                     {
                         InstallationLocationPicker.Watermark = x;
                         if (ViewModel?.Installer?.Location != null)
                             ViewModel.Installer.Location.TargetPath = (AbsolutePath)x;
                     })
                    .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.SuggestedDownloadFolder)
                     .ObserveOnGuiThread()
                     .Subscribe(x =>
                     {
                         DownloadLocationPicker.Watermark = x;
                         if (ViewModel?.Installer?.Location != null)
                             ViewModel.Installer.DownloadLocation.TargetPath = (AbsolutePath)x;
                     })
                    .DisposeWith(disposables);


            /*
            ViewModel.WhenAnyValue(vm => vm.Installer)
                     .Subscribe(x => {
                         x.Location.TargetPath = (AbsolutePath)InstallationLocationPicker.Watermark;
                         })
                     .DisposeWith(disposables);
            */

            /*
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

            ViewModel.WhenAnyValue(vm => vm.InstallState)
                .Select(v => v == InstallState.Installing ? Visibility.Collapsed : Visibility.Visible)
                .BindToStrict(this, view => view.BackButton.Visibility)
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
            ViewModel.WhenAnyValue(vm => vm.ProgressText)
                .ObserveOnGuiThread()
                .BindToStrict(this, view => view.TopProgressBar.Title)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.ProgressPercent)
                .ObserveOnGuiThread()
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

            ViewModel.WhenAnyValue(vm => vm.SlideShowImage)
                .BindToStrict(this, view => view.DetailImage.Image)
                .DisposeWith(disposables);
            */

        });
    }
}
