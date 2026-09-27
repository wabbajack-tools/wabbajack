using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Interfaces;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Avalonia.ViewModels.Gallery;

/// <summary>The floating pane a gallery tile opens: the list's description, links, install button and archives.</summary>
public partial class ModListDetailsVM : ViewModel, IClosableVM
{
    private readonly Client _wjClient;
    private readonly ILogger<ModListDetailsVM> _logger;

    private readonly SourceCache<Archive, Hash> _archives = new(a => a.Hash);
    private ReadOnlyObservableCollection<Archive> _filteredArchives = new(new ObservableCollection<Archive>());

    [Reactive] public partial BaseModListMetadataVM MetadataVM { get; set; }
    [Reactive] public partial string Search { get; set; }

    public ReadOnlyObservableCollection<Archive> Archives => _filteredArchives;

    public ICommand OpenWebsiteCommand { get; }
    public ICommand OpenDiscordCommand { get; }
    public ICommand OpenReadmeCommand { get; }
    public ICommand CloseCommand { get; }

    public ModListDetailsVM(ILogger<ModListDetailsVM> logger, Client wjClient)
    {
        _logger = logger;
        _wjClient = wjClient;

        MessageBus.Current.Listen<LoadModlistForDetails>()
            .Subscribe(msg => MetadataVM = msg.MetadataVM)
            .DisposeWith(CompositeDisposable);

        // All three are free text out of the modlist repository, so they go through UIUtils.OpenWebsite for
        // its scheme filter and its log line rather than straight to the shell.
        OpenWebsiteCommand = ReactiveCommand.Create(() => UIUtils.OpenWebsite(MetadataVM.Metadata.Links.WebsiteURL),
            this.WhenAnyValue(x => x.MetadataVM.Metadata.Links.WebsiteURL, x => !string.IsNullOrEmpty(x)).ObserveOn(RxApp.MainThreadScheduler));
        OpenDiscordCommand = ReactiveCommand.Create(() => UIUtils.OpenWebsite(MetadataVM.Metadata.Links.DiscordURL),
            this.WhenAnyValue(x => x.MetadataVM.Metadata.Links.DiscordURL, x => !string.IsNullOrEmpty(x)).ObserveOn(RxApp.MainThreadScheduler));
        OpenReadmeCommand = ReactiveCommand.Create(() => UIUtils.OpenWebsite(MetadataVM.Metadata.Links.Readme),
            this.WhenAnyValue(x => x.MetadataVM.Metadata.Links.Readme, x => !string.IsNullOrEmpty(x)).ObserveOn(RxApp.MainThreadScheduler));

        CloseCommand = ReactiveCommand.Create(() => ShowFloatingWindow.Send(FloatingScreenType.None));

        this.WhenActivated(disposables =>
        {
            LoadArchives(MetadataVM.Metadata.RepositoryName, MetadataVM.Metadata.Links.MachineURL).FireAndForget();

            var searchThrottle = TimeSpan.FromSeconds(0.5);

            var searchTextPredicates = this.ObservableForProperty(vm => vm.Search)
                .Throttle(searchThrottle, RxApp.MainThreadScheduler)
                .Select(change => change.Value?.Trim() ?? "")
                .StartWith(Search)
                .Select<string, Func<Archive, bool>>(txt =>
                {
                    if (string.IsNullOrWhiteSpace(txt)) return _ => true;
                    return item => item.State is Nexus nexus
                        ? nexus.Name.ContainsCaseInsensitive(txt) || item.Name.ContainsCaseInsensitive(txt)
                        : item.Name.ContainsCaseInsensitive(txt);
                });

            var searchSorter = this.WhenValueChanged(vm => vm.Search)
                .Throttle(searchThrottle, RxApp.MainThreadScheduler)
                .Select(s => SortExpressionComparer<Archive>
                    .Descending(a => a.State is Nexus nexus && nexus.Name?.StartsWith(s ?? "", StringComparison.InvariantCultureIgnoreCase) == true)
                    .ThenByDescending(a => a.Name?.StartsWith(s ?? "", StringComparison.InvariantCultureIgnoreCase) == true)
                    .ThenByDescending(a => a.Name?.Contains(s ?? "", StringComparison.InvariantCultureIgnoreCase) == true));

            _archives.Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Filter(searchTextPredicates)
                .SortAndBind(out _filteredArchives, searchSorter)
                .Subscribe()
                .DisposeWith(disposables);
            // Each activation binds a new collection, and the view reads it through this property.
            this.RaisePropertyChanged(nameof(Archives));

            MetadataVM.ProgressPercent = Percent.One;
        });
    }

    private async Task LoadArchives(string repo, string machineURL)
    {
        using var ll = LoadingLock.WithLoading();
        try
        {
            var validatedModlist = await _wjClient.GetDetailedStatus(repo, machineURL);
            var archives = validatedModlist.Archives.Select(a => a.Original).ToList();
            _archives.Edit(a =>
            {
                a.Clear();
                a.AddOrUpdate(archives);
            });
            ll.Succeed();
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception while loading archives: {0}", ex.ToString());
            ll.Fail();
        }
    }
}
