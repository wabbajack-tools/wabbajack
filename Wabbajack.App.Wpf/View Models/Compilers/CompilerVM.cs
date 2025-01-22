using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using Microsoft.Extensions.Logging;
using Wabbajack.Messages;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using DynamicData;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Extensions;
using Wabbajack.Installer;
using Wabbajack.LoginManagers;
using Wabbajack.Models;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack
{
    public enum CompilerState
    {
        Configuration,
        Compiling,
        Completed,
        Errored
    }

    public class CompilerVM : BackNavigatingVM, ICpuStatusVM
    {
        private const string LastSavedCompilerSettings = "last-saved-compiler-settings";
        private readonly DTOSerializer _dtos;
        private readonly SettingsManager _settingsManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CompilerVM> _logger;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly CompilerSettingsInferencer _inferencer;
        private readonly IEnumerable<INeedsLogin> _logins;
        private readonly DownloadDispatcher _downloadDispatcher;
        private readonly Client _wjClient;
        private AsyncLock _waitForLoginLock = new ();

        [Reactive] public string StatusText { get; set; }
        [Reactive] public Percent StatusProgress { get; set; }

        [Reactive] public CompilerState State { get; set; }

        [Reactive] public MO2CompilerVM SubCompilerVM { get; set; }

        // Paths 
        public FilePickerVM ModlistLocation { get; }
        public FilePickerVM DownloadLocation { get; }
        public FilePickerVM OutputLocation { get; }

        // Modlist Settings

        [Reactive] public string ModListName { get; set; }
        [Reactive] public string Version { get; set; }
        [Reactive] public string Author { get; set; }
        [Reactive] public string Description { get; set; }
        public FilePickerVM ModListImagePath { get; } = new();
        [Reactive] public ImageSource ModListImage { get; set; }
        [Reactive] public string Website { get; set; }
        [Reactive] public string Readme { get; set; }
        [Reactive] public bool IsNSFW { get; set; }
        [Reactive] public bool PublishUpdate { get; set; }
        [Reactive] public string MachineUrl { get; set; }
        [Reactive] public Game BaseGame { get; set; }
        [Reactive] public string SelectedProfile { get; set; }
        [Reactive] public AbsolutePath GamePath { get; set; }
        [Reactive] public bool IsMO2Compilation { get; set; }

        [Reactive] public RelativePath[] AlwaysEnabled { get; set; } = Array.Empty<RelativePath>();
        [Reactive] public RelativePath[] NoMatchInclude { get; set; } = Array.Empty<RelativePath>();
        [Reactive] public RelativePath[] Include { get; set; } = Array.Empty<RelativePath>();
        [Reactive] public RelativePath[] Ignore { get; set; } = Array.Empty<RelativePath>();

        [Reactive] public string[] OtherProfiles { get; set; } = Array.Empty<string>();

        [Reactive] public AbsolutePath Source { get; set; }

        public AbsolutePath SettingsOutputLocation => Source.Combine(ModListName).WithExtension(Ext.CompilerSettings);


        public ReactiveCommand<Unit, Unit> ExecuteCommand { get; }
        public ReactiveCommand<Unit, Unit> ReInferSettingsCommand { get; set; }

        public LogStream LoggerProvider { get; }
        public ReadOnlyObservableCollection<CPUDisplayVM> StatusList => _resourceMonitor.Tasks;

        [Reactive] public ErrorResponse ErrorState { get; private set; }

        public CompilerVM(ILogger<CompilerVM> logger, DTOSerializer dtos, SettingsManager settingsManager,
            IServiceProvider serviceProvider, LogStream loggerProvider, ResourceMonitor resourceMonitor,
            CompilerSettingsInferencer inferencer, Client wjClient, IEnumerable<INeedsLogin> logins, DownloadDispatcher downloadDispatcher) : base(logger)
        {
            _logger = logger;
            _dtos = dtos;
            _settingsManager = settingsManager;
            _serviceProvider = serviceProvider;
            LoggerProvider = loggerProvider;
            _resourceMonitor = resourceMonitor;
            _inferencer = inferencer;
            _wjClient = wjClient;
            _logins = logins;
            _downloadDispatcher = downloadDispatcher;

            StatusText = "Compiler Settings";
            StatusProgress = Percent.Zero;

            BackCommand =
                ReactiveCommand.CreateFromTask(async () =>
                {
                    await SaveSettingsFile();
                    NavigateToGlobal.Send(NavigateToGlobal.ScreenType.ModeSelectionView);
                });

            SubCompilerVM = new MO2CompilerVM(this);

            ExecuteCommand = ReactiveCommand.CreateFromTask(async () => await StartCompilation());
            ReInferSettingsCommand = ReactiveCommand.CreateFromTask(async () => await ReInferSettings(),
                this.WhenAnyValue(vm => vm.Source)
                    .ObserveOnGuiThread()
                    .Select(v => v != default)
                    .CombineLatest(this.WhenAnyValue(vm => vm.ModListName)
                        .ObserveOnGuiThread()
                        .Select(p => !string.IsNullOrWhiteSpace(p)))
                    .Select(v => v.First && v.Second));

            ModlistLocation = new FilePickerVM
            {
                ExistCheckOption = FilePickerVM.CheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.File,
                PromptTitle = "Select a config file or a modlist.txt file"
            };

            DownloadLocation = new FilePickerVM
            {
                ExistCheckOption = FilePickerVM.CheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Location where the downloads for this list are stored"
            };

            OutputLocation = new FilePickerVM
            {
                ExistCheckOption = FilePickerVM.CheckOptions.Off,
                PathType = FilePickerVM.PathTypeOptions.Folder,
                PromptTitle = "Location where the compiled modlist will be stored"
            };

            ModlistLocation.Filters.AddRange(new[]
            {
                new CommonFileDialogFilter("MO2 Modlist", "*" + Ext.Txt),
                new CommonFileDialogFilter("Compiler Settings File", "*" + Ext.CompilerSettings)
            });


            this.WhenActivated(disposables =>
            {
                State = CompilerState.Configuration;
                Disposable.Empty.DisposeWith(disposables);

                ModlistLocation.WhenAnyValue(vm => vm.TargetPath)
                    .Subscribe(p => InferModListFromLocation(p).FireAndForget())
                    .DisposeWith(disposables);


                this.WhenAnyValue(x => x.DownloadLocation.TargetPath)
                    .CombineLatest(this.WhenAnyValue(x => x.ModlistLocation.TargetPath),
                        this.WhenAnyValue(x => x.OutputLocation.TargetPath),
                        this.WhenAnyValue(x => x.DownloadLocation.ErrorState),
                        this.WhenAnyValue(x => x.ModlistLocation.ErrorState),
                        this.WhenAnyValue(x => x.OutputLocation.ErrorState),
                        this.WhenAnyValue(x => x.ModListName),
                        this.WhenAnyValue(x => x.Version))
                    .Select(_ => Validate())
                    .BindToStrict(this, vm => vm.ErrorState)
                    .DisposeWith(disposables);

                LoadLastSavedSettings().FireAndForget();
            });
        }


        private async Task ReInferSettings()
        {
            var newSettings = await _inferencer.InferModListFromLocation(
                Source.Combine("profiles", SelectedProfile, "modlist.txt"));

            if (newSettings == null)
            {
                _logger.LogError("Cannot infer settings");
                return;
            }

            Include = newSettings.Include;
            Ignore = newSettings.Ignore;
            AlwaysEnabled = newSettings.AlwaysEnabled;
            NoMatchInclude = newSettings.NoMatchInclude;
            OtherProfiles = newSettings.AdditionalProfiles;
        }

        private ErrorResponse Validate()
        {
            var errors = new List<ErrorResponse>();
            errors.Add(DownloadLocation.ErrorState);
            errors.Add(ModlistLocation.ErrorState);
            errors.Add(OutputLocation.ErrorState);
            return ErrorResponse.Combine(errors);
        }

        private async Task InferModListFromLocation(AbsolutePath path)
        {
            using var _ = LoadingLock.WithLoading();

            CompilerSettings settings;
            if (path == default) return;
            if (path.FileName.Extension == Ext.CompilerSettings)
            {
                await using var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                settings = (await _dtos.DeserializeAsync<CompilerSettings>(fs))!;
            }
            else if (path.FileName == "modlist.txt".ToRelativePath())
            {
                settings = await _inferencer.InferModListFromLocation(path);
                if (settings == null) return;
            }
            else
            {
                return;
            }

            BaseGame = settings.Game;
            ModListName = settings.ModListName;
            Version = settings.Version?.ToString() ?? "";
            Author = settings.ModListAuthor;
            Description = settings.Description;
            ModListImagePath.TargetPath = settings.ModListImage;
            Website = settings.ModListWebsite?.ToString() ?? "";
            Readme = settings.ModListReadme?.ToString() ?? "";
            IsNSFW = settings.ModlistIsNSFW;

            Source = settings.Source;
            DownloadLocation.TargetPath = settings.Downloads;
            if (settings.OutputFile.Extension == Ext.Wabbajack)
                settings.OutputFile = settings.OutputFile.Parent;
            OutputLocation.TargetPath = settings.OutputFile;
            SelectedProfile = settings.Profile;
            PublishUpdate = settings.PublishUpdate;
            MachineUrl = settings.MachineUrl;
            OtherProfiles = settings.AdditionalProfiles;
            AlwaysEnabled = settings.AlwaysEnabled;
            NoMatchInclude = settings.NoMatchInclude;
            Include = settings.Include;
            Ignore = settings.Ignore;
            if (path.FileName == "modlist.txt".ToRelativePath())
            {
                await SaveSettingsFile();
                await LoadLastSavedSettings();
            }
        }


        private async Task StartCompilation()
        {
            var tsk = Task.Run(async () =>
            {
                try
                {
                    await SaveSettingsFile();
                    var token = CancellationToken.None;
                    State = CompilerState.Compiling;

                    foreach (var downloader in await _downloadDispatcher.AllDownloaders([new Nexus()]))
                    {
                        _logger.LogInformation("Preparing {Name}", downloader.GetType().Name);
                        if (await downloader.Prepare())
                            continue;

                        var manager = _logins
                            .FirstOrDefault(l => l.LoginFor() == downloader.GetType());
                        if (manager == null)
                        {
                            _logger.LogError("Cannot install, could not prepare {Name} for downloading",
                                downloader.GetType().Name);
                            throw new Exception($"No way to prepare {downloader}");
                        }

                        RxApp.MainThreadScheduler.Schedule(manager, (_, _) =>
                        {
                            manager.TriggerLogin.Execute(null);
                            return Disposable.Empty;
                        });

                        while (true)
                        {
                            if (await downloader.Prepare())
                                break;
                            await Task.Delay(1000);
                        }
                    }

                    var mo2Settings = GetSettings();
                    mo2Settings.UseGamePaths = true;
                    if (mo2Settings.OutputFile.DirectoryExists())
                        mo2Settings.OutputFile = mo2Settings.OutputFile.Combine(mo2Settings.ModListName.ToRelativePath()
                            .WithExtension(Ext.Wabbajack));

                    if (PublishUpdate && !await RunPreflightChecks(token))
                    {
                        State = CompilerState.Errored;
                        return;
                    }

                    var compiler = MO2Compiler.Create(_serviceProvider, mo2Settings);

                    var events = Observable.FromEventPattern<StatusUpdate>(h => compiler.OnStatusUpdate += h,
                            h => compiler.OnStatusUpdate -= h)
                        .ObserveOnGuiThread()
                        .Debounce(TimeSpan.FromSeconds(0.5))
                        .Subscribe(update =>
                        {
                            var s = update.EventArgs;
                            StatusText = $"[Step {s.CurrentStep}] {s.StatusText}";
                            StatusProgress = s.StepProgress;
                        });


                    try
                    {
                        var result = await compiler.Begin(token);
                        if (!result)
                            throw new Exception("Compilation Failed");
                    }
                    finally
                    {
                        events.Dispose();
                    }

                    if (PublishUpdate)
                    {
                        _logger.LogInformation("Publishing List");
                        var downloadMetadata = _dtos.Deserialize<DownloadMetadata>(
                            await mo2Settings.OutputFile.WithExtension(Ext.Meta).WithExtension(Ext.Json)
                                .ReadAllTextAsync())!;
                        await _wjClient.PublishModlist(MachineUrl, System.Version.Parse(Version),
                            mo2Settings.OutputFile, downloadMetadata);
                    }

                    _logger.LogInformation("Compiler Finished");

                    RxApp.MainThreadScheduler.Schedule(_logger, (_, _) =>
                    {
                        StatusText = "Compilation Completed";
                        StatusProgress = Percent.Zero;
                        State = CompilerState.Completed;
                        return Disposable.Empty;
                    });
                }
                catch (Exception ex)
                {
                    RxApp.MainThreadScheduler.Schedule(_logger, (_, _) =>
                    {
                        StatusText = "Compilation Failed";
                        StatusProgress = Percent.Zero;

                        State = CompilerState.Errored;
                        _logger.LogError(ex, "Failed Compilation : {Message}", ex.Message);
                        return Disposable.Empty;
                    });
                }
            });

            await tsk;
        }

        private async Task<bool> RunPreflightChecks(CancellationToken token)
        {
            var lists = await _wjClient.GetMyModlists(token);
            if (!lists.Any(x => x.Equals(MachineUrl, StringComparison.InvariantCultureIgnoreCase)))
            {
                _logger.LogError("Preflight Check failed, list {MachineUrl} not found in any repository", MachineUrl);
                return false;
            }

            if (!System.Version.TryParse(Version, out var v))
            {
                _logger.LogError("Bad Version Number {Version}", Version);
                return false;
            }

            return true;
        }

        private async Task SaveSettingsFile()
        {
            if (Source == default) return;
            await using var st = SettingsOutputLocation.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(st, GetSettings(), _dtos.Options);

            await _settingsManager.Save(LastSavedCompilerSettings, SettingsOutputLocation);
        }

        private async Task LoadLastSavedSettings()
        {
            var lastPath = await _settingsManager.Load<AbsolutePath>(LastSavedCompilerSettings);
            if (lastPath == default || !lastPath.FileExists() ||
                lastPath.FileName.Extension != Ext.CompilerSettings) return;
            ModlistLocation.TargetPath = lastPath;
        }


        private CompilerSettings GetSettings()
        {
            System.Version.TryParse(Version, out var pversion);
            Uri.TryCreate(Website, UriKind.Absolute, out var websiteUri);

            return new CompilerSettings
            {
                ModListName = ModListName,
                ModListAuthor = Author,
                Version = pversion ?? new Version(),
                Description = Description,
                ModListReadme = Readme,
                ModListImage = ModListImagePath.TargetPath,
                ModlistIsNSFW = IsNSFW,
                ModListWebsite = websiteUri ?? new Uri("http://www.wabbajack.org"),
                Downloads = DownloadLocation.TargetPath,
                Source = Source,
                Game = BaseGame,
                PublishUpdate = PublishUpdate,
                MachineUrl = MachineUrl,
                Profile = SelectedProfile,
                UseGamePaths = true,
                OutputFile = OutputLocation.TargetPath,
                AlwaysEnabled = AlwaysEnabled,
                AdditionalProfiles = OtherProfiles,
                NoMatchInclude = NoMatchInclude,
                Include = Include,
                Ignore = Ignore
            };
        }

        #region ListOps

        public void AddOtherProfile(string profile)
        {
            OtherProfiles = (OtherProfiles ?? Array.Empty<string>()).Append(profile).Distinct().ToArray();
        }

        public void RemoveProfile(string profile)
        {
            OtherProfiles = OtherProfiles.Where(p => p != profile).ToArray();
        }

        public void AddAlwaysEnabled(RelativePath path)
        {
            AlwaysEnabled = (AlwaysEnabled ?? Array.Empty<RelativePath>()).Append(path).Distinct().ToArray();
        }

        public void RemoveAlwaysEnabled(RelativePath path)
        {
            AlwaysEnabled = AlwaysEnabled.Where(p => p != path).ToArray();
        }

        public void AddNoMatchInclude(RelativePath path)
        {
            NoMatchInclude = (NoMatchInclude ?? Array.Empty<RelativePath>()).Append(path).Distinct().ToArray();
        }

        public void RemoveNoMatchInclude(RelativePath path)
        {
            NoMatchInclude = NoMatchInclude.Where(p => p != path).ToArray();
        }

        public void AddInclude(RelativePath path)
        {
            Include = (Include ?? Array.Empty<RelativePath>()).Append(path).Distinct().ToArray();
        }

        public void RemoveInclude(RelativePath path)
        {
            Include = Include.Where(p => p != path).ToArray();
        }


        public void AddIgnore(RelativePath path)
        {
            Ignore = (Ignore ?? Array.Empty<RelativePath>()).Append(path).Distinct().ToArray();
        }

        public void RemoveIgnore(RelativePath path)
        {
            Ignore = Ignore.Where(p => p != path).ToArray();
        }

        #endregion
    }
}