using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Extensions;
using Wabbajack.App.Messages;
using Wabbajack.App.Models;
using Wabbajack.App.ViewModels;
using Wabbajack.DTOs.SavedSettings;
using Wabbajack.Paths;

namespace Wabbajack.App.Screens
{
    public class LauncherViewModel : ViewModelBase, IActivatableViewModel, IReceiver<ConfigureLauncher>
    {
        [Reactive]
        public AbsolutePath InstallFolder { get; set; }
        
        [Reactive]
        public IBitmap Image { get; set; }
        
        [Reactive]
        public InstallationConfigurationSetting? Setting { get; set; }
        
        public LauncherViewModel(InstallationStateManager manager)
        {
            Activator = new ViewModelActivator();
            
            this.WhenActivated(disposables =>
            {
                this.WhenAnyValue(v => v.InstallFolder)
                    .SelectAsync(disposables, async folder => await manager.GetByInstallFolder(folder))
                    .Where(v => v != null)
                    .BindTo(this, vm => vm.Setting)
                    .DisposeWith(disposables);

                this.WhenAnyValue(v => v.Setting)
                    .Where(v => v != default)
                    .Select(v => new Bitmap((v!.Image).ToString()))
                    .BindTo(this, vm => vm.Image)
                    .DisposeWith(disposables);
                
            });
        }

        public void Receive(ConfigureLauncher val)
        {
            InstallFolder = val.InstallFolder;
        }
    }
}