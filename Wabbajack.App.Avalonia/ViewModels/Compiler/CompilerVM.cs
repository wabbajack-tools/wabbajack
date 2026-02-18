using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.ViewModels.Compiler;

public enum CompilerState { Home, Configuration, Compiling, Completed, Errored }

public class CompilerVM : ViewModelBase
{
    private const string RecentSettingsKey = "compiler-recent-settings";

    private readonly ILogger<CompilerVM> _logger;
    private readonly DTOSerializer _dtos;
    private readonly SettingsManager _settingsManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly CompilerSettingsInferencer _inferencer;
    private readonly Wabbajack.Services.OSIntegrated.Configuration _configuration;

    private CancellationTokenSource? _cancellationTokenSource;

    // ── State ─────────────────────────────────────────────────────────────
    [Reactive] public CompilerState State { get; set; } = CompilerState.Home;

    // ── Configuration fields ──────────────────────────────────────────────
    [Reactive] public string SourcePath { get; set; } = "";
    [Reactive] public string Profile { get; set; } = "";
    [Reactive] public string DownloadsPath { get; set; } = "";
    [Reactive] public string OutputPath { get; set; } = "";
    [Reactive] public string ModListName { get; set; } = "";
    [Reactive] public string ModListAuthor { get; set; } = "";
    [Reactive] public string ModListDescription { get; set; } = "";
    [Reactive] public string ModListVersion { get; set; } = "1.0.0.0";
    [Reactive] public string ImagePath { get; set; } = "";
    [Reactive] public bool IsNSFW { get; set; }

    // ── Progress ──────────────────────────────────────────────────────────
    [Reactive] public string ProgressText { get; set; } = "";
    [Reactive] public Percent ProgressPercent { get; set; }

    // ── Recent settings list ──────────────────────────────────────────────
    [Reactive] public List<AbsolutePath> RecentSettings { get; private set; } = new();

    // ── Commands ──────────────────────────────────────────────────────────
    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand OpenLogFolderCommand { get; }
    public ICommand OpenOutputFolderCommand { get; }
    public ICommand LoadRecentCommand { get; }

    public CompilerVM(
        ILogger<CompilerVM> logger,
        DTOSerializer dtos,
        SettingsManager settingsManager,
        IServiceProvider serviceProvider,
        CompilerSettingsInferencer inferencer,
        Wabbajack.Services.OSIntegrated.Configuration configuration)
    {
        _logger = logger;
        _dtos = dtos;
        _settingsManager = settingsManager;
        _serviceProvider = serviceProvider;
        _inferencer = inferencer;
        _configuration = configuration;

        var canStart = this.WhenAnyValue(
            vm => vm.State,
            vm => vm.SourcePath,
            vm => vm.ModListName,
            (state, source, name) =>
                state == CompilerState.Configuration &&
                !string.IsNullOrWhiteSpace(source) &&
                !string.IsNullOrWhiteSpace(name));

        StartCommand = ReactiveCommand.Create(
            () => BeginCompile().FireAndForget(),
            canStart);

        CancelCommand = ReactiveCommand.Create(CancelCompile);

        BackCommand = ReactiveCommand.Create(() =>
        {
            _cancellationTokenSource?.Cancel();
            State = CompilerState.Home;
            ProgressText = "";
            ProgressPercent = Percent.Zero;
        });

        OpenLogFolderCommand = ReactiveCommand.Create(() =>
            UIUtils.OpenFolder(_configuration.LogLocation));

        OpenOutputFolderCommand = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrWhiteSpace(OutputPath))
            {
                var dir = ((AbsolutePath)OutputPath).Parent;
                if (dir != default) UIUtils.OpenFolder(dir);
            }
        });

        LoadRecentCommand = ReactiveCommand.Create<AbsolutePath>(path =>
            LoadFromSettingsFile(path).FireAndForget());

        LoadRecentSettings().FireAndForget();
    }

    // ── Public API for code-behind browse actions ─────────────────────────

    public async Task InferFromModlistTxt(AbsolutePath modlistTxtPath)
    {
        using var ll = LoadingLock.WithLoading();
        try
        {
            var settings = await _inferencer.InferModListFromLocation(modlistTxtPath);
            if (settings == null)
            {
                _logger.LogWarning("Could not infer settings from {Path}", modlistTxtPath);
                return;
            }
            PopulateFromSettings(settings);
            State = CompilerState.Configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While inferring from modlist.txt");
        }
    }

    public async Task LoadFromSettingsFile(AbsolutePath settingsPath)
    {
        using var ll = LoadingLock.WithLoading();
        try
        {
            await using var stream = settingsPath.Open(System.IO.FileMode.Open, System.IO.FileAccess.Read);
            var settings = await _dtos.DeserializeAsync<CompilerSettings>(stream);
            if (settings == null) return;
            PopulateFromSettings(settings);
            State = CompilerState.Configuration;
            await AddToRecent(settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While loading compiler settings from {Path}", settingsPath);
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────

    private void PopulateFromSettings(CompilerSettings cs)
    {
        SourcePath = cs.Source.ToString();
        Profile = cs.Profile ?? "";
        DownloadsPath = cs.Downloads.ToString();
        OutputPath = cs.OutputFile.ToString();
        ModListName = cs.ModListName ?? "";
        ModListAuthor = cs.ModListAuthor ?? "";
        ModListDescription = cs.ModListDescription ?? "";
        ModListVersion = cs.Version?.ToString() ?? "1.0.0.0";
        ImagePath = cs.ModListImage == default ? "" : cs.ModListImage.ToString();
        IsNSFW = cs.ModlistIsNSFW;
    }

    private CompilerSettings BuildSettings()
    {
        System.Version.TryParse(ModListVersion, out var version);
        return new CompilerSettings
        {
            Source = string.IsNullOrWhiteSpace(SourcePath) ? default : (AbsolutePath)SourcePath,
            Downloads = string.IsNullOrWhiteSpace(DownloadsPath) ? default : (AbsolutePath)DownloadsPath,
            OutputFile = string.IsNullOrWhiteSpace(OutputPath) ? default : (AbsolutePath)OutputPath,
            ModListImage = string.IsNullOrWhiteSpace(ImagePath) ? default : (AbsolutePath)ImagePath,
            ModListName = ModListName,
            ModListAuthor = ModListAuthor,
            ModListDescription = ModListDescription,
            ModlistIsNSFW = IsNSFW,
            Profile = Profile,
            Version = version ?? new System.Version(1, 0, 0, 0),
        };
    }

    private async Task BeginCompile()
    {
        var settings = BuildSettings();

        RxApp.MainThreadScheduler.Schedule(() =>
        {
            State = CompilerState.Compiling;
            ProgressText = $"Starting compilation of {ModListName}…";
            ProgressPercent = Percent.Zero;
        });

        // Persist settings for future loading
        if (!string.IsNullOrWhiteSpace(settings.OutputFile.ToString()))
        {
            var settingsPath = settings.Source.Combine(settings.ModListName)
                .WithExtension(new Extension(".wabbajack_compiler_settings"));
            try
            {
                await using var stream = settingsPath.Open(System.IO.FileMode.Create, System.IO.FileAccess.Write);
                await _dtos.Serialize(settings, stream, true);
                await AddToRecent(settingsPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not save compiler settings");
            }
        }

        await Task.Run(async () =>
        {
            try
            {
                var compiler = MO2Compiler.Create(_serviceProvider, settings);
                compiler.OnStatusUpdate += (_, update) =>
                {
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        ProgressText = update.StatusText;
                        ProgressPercent = update.StepsProgress;
                    });
                };

                _logger.LogInformation("Starting compilation of {Name}", ModListName);

                bool success;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    success = await compiler.Begin(_cancellationTokenSource.Token);
                }

                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    if (success)
                    {
                        ProgressText = $"Successfully compiled {ModListName}";
                        ProgressPercent = Percent.One;
                        State = CompilerState.Completed;
                    }
                    else
                    {
                        ProgressText = $"Compilation of {ModListName} failed";
                        State = CompilerState.Errored;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    ProgressText = "Compilation cancelled";
                    State = CompilerState.Configuration;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "During compilation");
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    ProgressText = $"Error: {ex.Message}";
                    State = CompilerState.Errored;
                });
            }
        });
    }

    private void CancelCompile()
    {
        if (_cancellationTokenSource is { IsCancellationRequested: false })
            _cancellationTokenSource.CancelAsync().FireAndForget();
    }

    private async Task LoadRecentSettings()
    {
        var paths = await _settingsManager.Load<List<string>>(RecentSettingsKey) ?? new List<string>();
        RecentSettings = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => (AbsolutePath)p)
            .Where(p => p.FileExists())
            .ToList() ?? new List<AbsolutePath>();
    }

    private async Task AddToRecent(AbsolutePath path)
    {
        var paths = await _settingsManager.Load<List<string>>(RecentSettingsKey) ?? new List<string>();
        var pathStr = path.ToString();
        paths.Remove(pathStr);
        paths.Insert(0, pathStr);
        if (paths.Count > 10) paths.RemoveRange(10, paths.Count - 10);
        await _settingsManager.Save(RecentSettingsKey, paths);
        await LoadRecentSettings();
    }
}
