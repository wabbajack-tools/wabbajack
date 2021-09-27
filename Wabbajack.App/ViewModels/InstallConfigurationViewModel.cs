using System.IO;
using System.IO.Compression;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Extensions;
using Wabbajack.App.Extensions;
using Wabbajack.App.Messages;
using Wabbajack.App.Models;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.SavedSettings;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.ViewModels
{
    public class InstallConfigurationViewModel : ViewModelBase, IActivatableViewModel, IReceiver<StartInstallConfiguration>
    {
        private readonly DTOSerializer _dtos;
        private readonly InstallationStateManager _stateManager;

        [Reactive]
        public AbsolutePath ModListPath { get; set; }
        
        [Reactive]
        public AbsolutePath Install { get; set; }
        
        [Reactive]
        public AbsolutePath Download { get; set; }
        
        [Reactive]
        public ModList? ModList { get; set; }
        
        [Reactive]
        public IBitmap? ModListImage { get; set; }
        
        [Reactive]
        public bool IsReady { get; set; }
        
        [Reactive]
        public ReactiveCommand<Unit, Unit> BeginCommand { get; set; }
        
        

        public InstallConfigurationViewModel(DTOSerializer dtos, InstallationStateManager stateManager)
        {
            _stateManager = stateManager;

            _dtos = dtos;
            Activator = new ViewModelActivator();
            this.WhenActivated(disposables =>
            {

                this.ValidationRule(x => x.ModListPath, p => p.FileExists(), "Wabbajack file must exist");
                this.ValidationRule(x => x.Install, p => p.DirectoryExists(), "Install folder file must exist");
                this.ValidationRule(x => x.Download, p => p != default, "Download folder must be set");
                
                BeginCommand = ReactiveCommand.Create(StartInstall, this.IsValid());
                

                this.WhenAnyValue(t => t.ModListPath)
                    .Where(t => t != default)
                    .SelectAsync(disposables, async x => await LoadModList(x))
                    .Select(x => x)
                    .ObserveOn(AvaloniaScheduler.Instance)
                    .BindTo(this, t => t.ModList)
                    .DisposeWith(disposables);

                this.WhenAnyValue(t => t.ModListPath)
                    .Where(t => t != default)
                    .SelectAsync(disposables, async x => await LoadModListImage(x))
                    .ObserveOn(AvaloniaScheduler.Instance)
                    .BindTo(this, t => t.ModListImage)
                    .DisposeWith(disposables);  
                
                SetupDefaults().FireAndForget();
            });


        }

        private async Task SetupDefaults()
        {
            var lastState = await _stateManager.GetLastState();
            if (!lastState.ModList.FileExists()) return;
            await Task.Delay(250); // Ugly hack
            ModListPath = lastState.ModList;
            Install = lastState.Install;
            Download = lastState.Downloads;
        }

        private void StartInstall()
        {
            _stateManager.SetLastState(new InstallationConfigurationSetting
            {
                ModList = ModListPath,
                Downloads = Download,
                Install = Install
            }).FireAndForget();
            
            MessageBus.Instance.Send(new NavigateTo(typeof(StandardInstallationViewModel)));
            MessageBus.Instance.Send(new StartInstallation(ModListPath, Install, Download));
        }

        private async Task<IBitmap> LoadModListImage(AbsolutePath path)
        {
            await using var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            using var ar = new ZipArchive(fs, ZipArchiveMode.Read);
            var entry = ar.GetEntry("modlist-image.png");
            await using var stream = entry!.Open();
            return new Bitmap(new MemoryStream(await stream.ReadAllAsync()));
        }

        private async Task<ModList> LoadModList(AbsolutePath modlist)
        { 
            var definition= await StandardInstaller.LoadFromFile(_dtos, modlist);
            return definition;
        }
        
        public ViewModelActivator Activator { get; }
        
        public void Receive(StartInstallConfiguration val)
        {
            ModListPath = val.ModList;
        }
    }
}