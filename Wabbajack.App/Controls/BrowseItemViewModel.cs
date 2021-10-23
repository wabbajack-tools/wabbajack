using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Messages;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.App.Controls;

public enum ModListState
{
    Downloaded,
    NotDownloaded,
    Downloading
}

public class BrowseItemViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly HttpClient _client;
    private readonly Configuration _configuration;
    private readonly DownloadDispatcher _dispatcher;
    private readonly IResource<DownloadDispatcher> _downloadLimiter;
    private readonly DTOSerializer _dtos;
    private readonly FileHashCache _hashCache;
    private readonly IResource<HttpClient> _limiter;
    private readonly ILogger _logger;
    private readonly ModlistMetadata _metadata;
    private readonly ModListSummary _summary;

    public BrowseItemViewModel(ModlistMetadata metadata, ModListSummary summary, HttpClient client,
        IResource<HttpClient> limiter,
        FileHashCache hashCache, Configuration configuration, DownloadDispatcher dispatcher,
        IResource<DownloadDispatcher> downloadLimiter, GameLocator gameLocator,
        DTOSerializer dtos, ILogger logger)
    {
        Activator = new ViewModelActivator();
        _metadata = metadata;
        _summary = summary;
        _client = client;
        _limiter = limiter;
        _hashCache = hashCache;
        _configuration = configuration;
        _dispatcher = dispatcher;
        _downloadLimiter = downloadLimiter;
        _logger = logger;
        _dtos = dtos;

        var haveGame = gameLocator.IsInstalled(_metadata.Game);
        Tags = metadata.tags
            .Select(t => new TagViewModel(t, "ModList"))
            .Prepend(new TagViewModel(_metadata.Game.MetaData().HumanFriendlyGameName,
                haveGame ? "Game" : "GameNotInstalled"))
            .ToArray();

        OpenWebsiteCommand = ReactiveCommand.Create(() =>
        {
            Utils.OpenWebsiteInExternalBrowser(new Uri(_metadata.Links.Readme));
        });

        ExecuteCommand = ReactiveCommand.Create(() =>
            {
                if (State == ModListState.Downloaded)
                {
                    MessageBus.Instance.Send(new StartInstallConfiguration(ModListLocation));
                    MessageBus.Instance.Send(new NavigateTo(typeof(InstallConfigurationViewModel)));
                }
                else
                {
                    DownloadModList().FireAndForget();
                }
            },
            this.ObservableForProperty(t => t.State)
                .Select(c => c.Value != ModListState.Downloading)
                .StartWith(true));

        LoadListImage().FireAndForget();
        UpdateState().FireAndForget();
    }

    public string Title => _metadata.ImageContainsTitle ? "" : _metadata.Title;
    public string MachineURL => _metadata.Links.MachineURL;
    public string Description => _metadata.Description;

    public Uri ImageUri => new(_metadata.Links.ImageUri);

    [Reactive] public IBitmap Image { get; set; }

    [Reactive] public ModListState State { get; set; }

    [Reactive] public ReactiveCommand<Unit, Unit> ExecuteCommand { get; set; }

    [Reactive] public Percent Progress { get; set; }

    public AbsolutePath ModListLocation => _configuration.ModListsDownloadLocation.Combine(_metadata.Links.MachineURL)
        .WithExtension(Ext.Wabbajack);

    public Game Game => _metadata.Game;

    public bool IsUtilityList => _metadata.UtilityList;
    public bool IsNSFW => _metadata.NSFW;

    [Reactive] public TagViewModel[] Tags { get; set; }


    public ReactiveCommand<Unit, Unit> OpenWebsiteCommand { get; set; }

    private async Task DownloadModList()
    {
        State = ModListState.Downloading;
        var state = _dispatcher.Parse(new Uri(_metadata.Links.Download));
        var archive = new Archive
        {
            State = state!,
            Hash = _metadata.DownloadMetadata?.Hash ?? default,
            Size = _metadata.DownloadMetadata?.Size ?? 0,
            Name = ModListLocation.FileName.ToString()
        };

        using var job = await _downloadLimiter.Begin(state!.PrimaryKeyString, archive.Size, CancellationToken.None);

        var hashTask = _dispatcher.Download(archive, ModListLocation, job, CancellationToken.None);

        while (!hashTask.IsCompleted)
        {
            Progress = Percent.FactoryPutInRange(job.Current, job.Size ?? 0);
            await Task.Delay(100);
        }

        var hash = await hashTask;
        if (hash != _metadata.DownloadMetadata?.Hash)
        {
            _logger.LogWarning("Hash files didn't match after downloading modlist, deleting modlist");
            if (ModListLocation.FileExists())
                ModListLocation.Delete();
        }

        _hashCache.FileHashWriteCache(ModListLocation, hash);

        var metadataPath = ModListLocation.WithExtension(Ext.MetaData);
        await metadataPath.WriteAllTextAsync(_dtos.Serialize(_metadata));

        await UpdateState();
    }


    public async Task LoadListImage()
    {
        using var job = await _limiter.Begin("Loading modlist image", 0, CancellationToken.None);
        var response = await _client.GetByteArrayAsync(ImageUri);
        Image = new Bitmap(new MemoryStream(response));
    }

    public async Task<ModListState> GetState()
    {
        var file = ModListLocation;
        if (!file.FileExists())
            return ModListState.NotDownloaded;

        return await _hashCache.FileHashCachedAsync(file, CancellationToken.None) !=
               _metadata.DownloadMetadata?.Hash
            ? ModListState.NotDownloaded
            : ModListState.Downloaded;
    }

    public async Task UpdateState()
    {
        State = await GetState();
    }
}