using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack
{
    public class CreateModListVM : ViewModel
    {
        private readonly SettingsManager _settingsManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CreateModListVM> _logger;
        private readonly Client _wjClient;

        private readonly ModListDownloadMaintainer _maintainer;
        private readonly CancellationToken _cancellationToken;

        private readonly SourceCache<CreateModListMetadataVM, string> _modLists = new(x => x.Metadata.NamespacedName);
        public ReadOnlyObservableCollection<CreateModListMetadataVM> _filteredModLists;

        public ReadOnlyObservableCollection<CreateModListMetadataVM> ModLists => _filteredModLists;
        
        public CreateModListVM(ILogger<CreateModListVM> logger, SettingsManager settingsManager,
            IServiceProvider serviceProvider, Client wjClient, ModListDownloadMaintainer maintainer)
        {
            _logger = logger;
            _settingsManager = settingsManager;
            _serviceProvider = serviceProvider;
            _wjClient = wjClient;
            _maintainer = maintainer;

            LoadModLists().FireAndForget();
            
            this.WhenActivated(disposables =>
            {
                _modLists.Connect()
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Bind(out _filteredModLists)
                    .Subscribe((_) => { })
                    .DisposeWith(disposables);
            });
        }
        private async Task LoadModLists()
        {
            using var ll = LoadingLock.WithLoading();
            try
            {
                var modLists = await _wjClient.LoadLists();
                _modLists.Edit(e =>
                {
                    e.Clear();
                    e.AddOrUpdate(modLists.Select(m =>
                        new CreateModListMetadataVM(_logger, this, m, _maintainer, _wjClient, _cancellationToken)));
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While loading lists");
                ll.Fail();
            }
            ll.Succeed();
        }
    }
}
