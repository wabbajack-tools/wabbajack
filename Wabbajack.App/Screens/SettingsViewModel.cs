using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.App.Messages;
using Wabbajack.App.ViewModels;
using Wabbajack.Paths;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack.App.Screens
{
    public class SettingsViewModel : ViewModelBase, IReceiverMarker
    {
        private readonly ILogger<SettingsViewModel> _logger;
        
        public ReactiveCommand<Unit, Unit> NexusLogin { get; set; }
        public ReactiveCommand<Unit, Unit> NexusLogout { get; set; }
        
        public FileSystemWatcher Watcher { get; set; }

        private readonly Subject<AbsolutePath> _fileSystemEvents = new();
        
        public SettingsViewModel(ILogger<SettingsViewModel> logger, Configuration configuration, NexusApiTokenProvider nexusProvider)
        {
            _logger = logger;
            Activator = new ViewModelActivator();
            
            this.WhenActivated(disposables =>
            {
                Watcher = new FileSystemWatcher(configuration.EncryptedDataLocation.ToString());
                Watcher.DisposeWith(disposables);
                Watcher.Created += Pulse;
                Watcher.Deleted += Pulse;
                Watcher.Renamed += Pulse;
                Watcher.Changed += Pulse;
                
                Watcher.EnableRaisingEvents = true;

                var haveNexusToken = this._fileSystemEvents
                    .StartWith(AbsolutePath.Empty)
                    .Select(_ => nexusProvider.HaveToken());

                NexusLogin = ReactiveCommand.Create(() =>
                {
                    MessageBus.Instance.Send(new NavigateTo(typeof(NexusLoginViewModel)));
                }, haveNexusToken.Select(x => !x));
                NexusLogout = ReactiveCommand.Create(nexusProvider.DeleteToken, haveNexusToken.Select(x => x));
                


            });
        }

        private void Pulse(object sender, FileSystemEventArgs e)
        {
            _fileSystemEvents.OnNext(e.Name?.ToAbsolutePath() ?? default);
        }
    }
}