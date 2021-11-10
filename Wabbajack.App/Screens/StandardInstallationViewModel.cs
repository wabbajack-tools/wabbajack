using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Messages;
using Wabbajack.App.Models;
using Wabbajack.App.Screens;
using Wabbajack.App.Utilities;
using Wabbajack.App.ViewModels.SubViewModels;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.SavedSettings;
using Wabbajack.Installer;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.ViewModels;

public class StandardInstallationViewModel : ViewModelBase
{
    private readonly DTOSerializer _dtos;
    private readonly HttpClient _httpClient;
    private readonly InstallationStateManager _installStateManager;
    private readonly GameLocator _locator;
    private readonly ILogger<StandardInstallationViewModel> _logger;
    private readonly IServiceProvider _provider;
    private InstallerConfiguration _config;
    private int _currentSlideIndex;
    private StandardInstaller _installer;
    private IServiceScope _scope;
    private SlideViewModel[] _slides = Array.Empty<SlideViewModel>();
    private Timer _slideTimer;
    private Timer _updateTimer;

    public StandardInstallationViewModel(ILogger<StandardInstallationViewModel> logger, IServiceProvider provider,
        GameLocator locator, DTOSerializer dtos,
        HttpClient httpClient, InstallationStateManager manager)
    {
        _provider = provider;
        _locator = locator;
        _logger = logger;
        _dtos = dtos;
        _httpClient = httpClient;
        _installStateManager = manager;
        Activator = new ViewModelActivator();

        MessageBus.Current.Listen<StartInstallation>()
            .Subscribe(Receive)
            .DisposeWith(VMDisposables);

        this.WhenActivated(disposables =>
        {
            _updateTimer = new Timer(UpdateStatus, null, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(100));
            _updateTimer.DisposeWith(disposables);
            
            _slideTimer = new Timer(_ =>
            {
                if (IsPlaying) NextSlide(1);
            }, null, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(5));

            _currentSlideIndex = 0;
            _slideTimer.DisposeWith(disposables);

            NextCommand = ReactiveCommand.Create(() => NextSlide(1))
                .DisposeWith(disposables);
            PrevCommand = ReactiveCommand.Create(() => NextSlide(-1))
                .DisposeWith(disposables);
            PauseCommand = ReactiveCommand.Create(() => IsPlaying = false,
                    this.ObservableForProperty(vm => vm.IsPlaying, skipInitial: false)
                        .Select(v => v.Value))
                .DisposeWith(disposables);

            PlayCommand = ReactiveCommand.Create(() => IsPlaying = true,
                    this.ObservableForProperty(vm => vm.IsPlaying, skipInitial: false)
                        .Select(v => !v.Value))
                .DisposeWith(disposables);
        });
    }

    [Reactive] public SlideViewModel Slide { get; set; }

    [Reactive] public ReactiveCommand<Unit, Unit> NextCommand { get; set; }

    [Reactive] public ReactiveCommand<Unit, Unit> PrevCommand { get; set; }

    [Reactive] public ReactiveCommand<Unit, bool> PauseCommand { get; set; }

    [Reactive] public ReactiveCommand<Unit, bool> PlayCommand { get; set; }

    [Reactive] public bool IsPlaying { get; set; } = true;

    [Reactive] public string StatusText { get; set; } = "";
    [Reactive] public Percent StepsProgress { get; set; } = Percent.Zero;
    [Reactive] public Percent StepProgress { get; set; } = Percent.Zero;
    
    // Not Reactive, so we don't end up spamming the UI threads with events
    public StatusUpdate _latestStatus = new("", Percent.Zero, Percent.Zero);

    public void Receive(StartInstallation msg)
    {
        Install(msg).FireAndForget();
    }

    private void UpdateStatus(object? state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StepsProgress = _latestStatus.StepsProgress;
            StepProgress = _latestStatus.StepProgress;
            StatusText = _latestStatus.StatusText;
        }, DispatcherPriority.Render);
    }

    private void NextSlide(int direction)
    {
        if (_slides.Length == 0) return;
        _currentSlideIndex = InSlideRange(_currentSlideIndex + direction);

        var thisSlide = _slides[_currentSlideIndex];

        if (thisSlide.Image == null)
            thisSlide.PreCache(_httpClient).FireAndForget();

        // Cache the next image
        _slides[InSlideRange(_currentSlideIndex + 1)].PreCache(_httpClient).FireAndForget();

        var prevSlide = _slides[InSlideRange(_currentSlideIndex - 2)];
        //if (prevSlide.Image != null)
        //    prevSlide.Image = null;

        Dispatcher.UIThread.InvokeAsync(() => { Slide = thisSlide; });
    }

    private int InSlideRange(int i)
    {
        while (!(i >= 0 && i <= _slides.Length))
        {
            if (i >= _slides.Length) i -= _slides.Length;
            if (i < 0) i += _slides.Length;
        }

        return i;
    }

    private async Task Install(StartInstallation msg)
    {
        _scope = _provider.CreateScope();
        _config = _provider.GetService<InstallerConfiguration>()!;
        _config.Downloads = msg.Download;
        _config.Install = msg.Install;
        _config.ModlistArchive = msg.ModListPath;
        _config.Metadata = msg.Metadata;

        _logger.LogInformation("Loading ModList Data");
        _config.ModList = await StandardInstaller.LoadFromFile(_dtos, msg.ModListPath);
        _config.Game = _config.ModList.GameType;

        _slides = _config.ModList.Archives.Select(a => a.State).OfType<IMetaState>()
            .Select(m => new SlideViewModel {MetaState = m})
            .Where(s => !s.MetaState.IsNSFW)
            .Shuffle()
            .ToArray();

        _slides[1].PreCache(_httpClient).FireAndForget();
        Slide = _slides[1];

        if (_config.GameFolder == default)
        {
            if (!_locator.TryFindLocation(_config.Game, out var found))
            {
                _logger.LogCritical("Game {game} is not installed on this system",
                    _config.Game.MetaData().HumanFriendlyGameName);
                throw new Exception("Game not found");
            }

            _config.GameFolder = found;
        }

        _installer = _provider.GetService<StandardInstaller>()!;

        _installer.OnStatusUpdate = update => _latestStatus = update;

        _logger.LogInformation("Installer created, starting the installation process");
        try
        {
            var result = await Task.Run(async () => await _installer.Begin(CancellationToken.None));
            if (!result) throw new Exception("Installation failed");

            if (result) await SaveConfigAndContinue(_config);
        }
        catch (Exception ex)
        {
            ErrorPageViewModel.Display("During installation", ex);
        }
    }


    private async Task SaveConfigAndContinue(InstallerConfiguration config)
    {
        var path = config.Install.Combine("modlist-image.png");
        {
            var image = await ModListUtilities.GetModListImageStream(config.ModlistArchive);
            await using var os = path.Open(FileMode.Create, FileAccess.Write);
            await image.CopyToAsync(os);
        }

        await _installStateManager.SetLastState(new InstallationConfigurationSetting
        {
            Downloads = config.Downloads,
            Install = config.Install,
            Metadata = config.Metadata,
            ModList = config.ModlistArchive,
            Image = path
        });

        MessageBus.Current.SendMessage(new ConfigureLauncher(config.Install));
        MessageBus.Current.SendMessage(new NavigateTo(typeof(LauncherViewModel)));
    }
}