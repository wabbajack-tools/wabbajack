using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Wpf;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.ModListValidation;
using Wabbajack.DTOs.ServerResponses;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Messages;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;

namespace Wabbajack;

public class ModListDetailsVM : BackNavigatingVM
{
    private readonly Client _wjClient;
    [Reactive]
    public BaseModListMetadataVM MetadataVM { get; set; }

    [Reactive]
    public ValidatedModList ValidatedModlist { get; set; }

    [Reactive]
    public ObservableCollection<DetailedStatusItem> Status { get; set; }
    
    [Reactive]
    public string Search { get; set; }

    private readonly SourceCache<Archive, Hash> _archives = new(a => a.Hash);
    private ReadOnlyObservableCollection<Archive> _filteredArchives;
    public ReadOnlyObservableCollection<Archive> Archives => _filteredArchives;

    private readonly ILogger<ModListDetailsVM> _logger;

    public ICommand OpenWebsiteCommand { get; set; }
    public ICommand OpenDiscordCommand { get; set; }
    public ICommand OpenReadmeCommand { get; set; }

    public WebView2 Browser { get; set; }

    public ModListDetailsVM(ILogger<ModListDetailsVM> logger, IServiceProvider serviceProvider, Client wjClient) : base(logger)
    {
        _logger = logger;
        _wjClient = wjClient;

        Browser = serviceProvider.GetRequiredService<WebView2>();

        MessageBus.Current.Listen<LoadModlistForDetails>()
            .Subscribe(msg => MetadataVM = msg.MetadataVM)
            .DisposeWith(CompositeDisposable);

        OpenWebsiteCommand = ReactiveCommand.Create(() => Process.Start(new ProcessStartInfo(MetadataVM.Metadata.Links.WebsiteURL) { UseShellExecute = true }),
            this.WhenAnyValue(x => x.MetadataVM.Metadata.Links.WebsiteURL, x => !string.IsNullOrEmpty(x)).ObserveOnGuiThread());
        OpenDiscordCommand = ReactiveCommand.Create(() => Process.Start(new ProcessStartInfo(MetadataVM.Metadata.Links.DiscordURL) { UseShellExecute = true }),
            this.WhenAnyValue(x => x.MetadataVM.Metadata.Links.DiscordURL, x => !string.IsNullOrEmpty(x)).ObserveOnGuiThread());
        OpenReadmeCommand = ReactiveCommand.Create(() => Process.Start(new ProcessStartInfo(MetadataVM.Metadata.Links.Readme) { UseShellExecute = true }),
            this.WhenAnyValue(x => x.MetadataVM.Metadata.Links.Readme, x => !string.IsNullOrEmpty(x)).ObserveOnGuiThread());

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
                    return item => item.State is Nexus nexus ? nexus.Name.ContainsCaseInsensitive(txt) : item.Name.ContainsCaseInsensitive(txt);
                });

            var searchSorter = this.WhenValueChanged(vm => vm.Search)
                                    .Throttle(searchThrottle, RxApp.MainThreadScheduler)
                                    .Select(s => SortExpressionComparer<Archive>
                                                 .Descending(a => a.State is Nexus ? ((Nexus)a.State).Name?.StartsWith(s ?? "", StringComparison.InvariantCultureIgnoreCase) : false)
                                                 .ThenByDescending(a => a.Name?.StartsWith(s ?? "", StringComparison.InvariantCultureIgnoreCase))
                                                 .ThenByDescending(a => a.Name?.Contains(s ?? "", StringComparison.InvariantCultureIgnoreCase)));

            _archives.Connect()
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Filter(searchTextPredicates)
                    .Sort(searchSorter)
                    .TreatMovesAsRemoveAdd()
                    .Bind(out _filteredArchives)
                    .Subscribe()
                    .DisposeWith(disposables);

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
        catch(Exception ex)
        {
            _logger.LogError("Exception while loading archives: {0}", ex.ToString());
            ll.Fail();
        }
    }
}
