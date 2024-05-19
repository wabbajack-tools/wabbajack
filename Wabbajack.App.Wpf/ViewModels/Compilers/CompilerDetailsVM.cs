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
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Extensions;
using Wabbajack.Installer;
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
    public class CompilerDetailsVM : BackNavigatingVM, ICpuStatusVM
    {
        private readonly DTOSerializer _dtos;
        private readonly SettingsManager _settingsManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CompilerDetailsVM> _logger;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly CompilerSettingsInferencer _inferencer;
        private readonly Client _wjClient;
        
        [Reactive] public string StatusText { get; set; }
        [Reactive] public Percent StatusProgress { get; set; }

        [Reactive]
        public CompilerState State { get; set; }
        
        [Reactive]
        public MO2CompilerVM SubCompilerVM { get; set; }
        
        // Paths 
        public FilePickerVM ModlistLocation { get; }
        public FilePickerVM DownloadLocation { get; }
        public FilePickerVM OutputLocation { get; }

        [Reactive] public CompilerSettingsVM Settings { get; set; } = new();

        public FilePickerVM ModListImageLocation { get; } = new();
        
        public ReactiveCommand<Unit, Unit> ExecuteCommand { get; }
        public ReactiveCommand<Unit, Unit> ReInferSettingsCommand { get; set; }

        public LogStream LoggerProvider { get; }
        public ReadOnlyObservableCollection<CPUDisplayVM> StatusList => _resourceMonitor.Tasks;
        
        [Reactive]
        public ErrorResponse ErrorState { get; private set; }
        
        public CompilerDetailsVM(ILogger<CompilerDetailsVM> logger, DTOSerializer dtos, SettingsManager settingsManager,
            IServiceProvider serviceProvider, LogStream loggerProvider, ResourceMonitor resourceMonitor, 
            CompilerSettingsInferencer inferencer, Client wjClient) : base(logger)
        {
            _logger = logger;
            _dtos = dtos;
            _settingsManager = settingsManager;
            _serviceProvider = serviceProvider;
            LoggerProvider = loggerProvider;
            _resourceMonitor = resourceMonitor;
            _inferencer = inferencer;
            _wjClient = wjClient;

            MessageBus.Current.Listen<LoadModlistForCompiling>()
                .Subscribe(msg => {
                    var csVm = new CompilerSettingsVM(msg.CompilerSettings);
                    ModlistLocation.TargetPath = csVm.ProfilePath;
                    ModListImageLocation.TargetPath = csVm.ModListImage;
                    DownloadLocation.TargetPath = csVm.Downloads;
                    OutputLocation.TargetPath = csVm.OutputFile;
                    Settings = csVm;
                })
                .DisposeWith(CompositeDisposable);

            StatusText = "Compiler Settings";
            StatusProgress = Percent.Zero;

            BackCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await SaveSettingsFile();
                NavigateToGlobal.Send(ScreenType.Home);
            });

            SubCompilerVM = new MO2CompilerVM(this);

            ExecuteCommand = ReactiveCommand.CreateFromTask(async () => await StartCompilation());
            /*ReInferSettingsCommand = ReactiveCommand.CreateFromTask(async () => await ReInferSettings(),

                this.WhenAnyValue(vm => vm.Settings.Source)
                    .ObserveOnGuiThread()
                    .Select(v => v != default)
                    .CombineLatest(this.WhenAnyValue(vm => vm.Settings.ModListName)
                        .ObserveOnGuiThread()
                        .Select(p => !string.IsNullOrWhiteSpace(p)))
                    .Select(v => v.First && v.Second));
            */

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

            ModlistLocation.Filters.AddRange(new []
            {
                new CommonFileDialogFilter("MO2 Modlist", "*" + Ext.Txt),
                new CommonFileDialogFilter("Compiler Settings File", "*" + Ext.CompilerSettings)
            });

            
            this.WhenActivated(disposables =>
            {
                State = CompilerState.Configuration;
                Disposable.Empty.DisposeWith(disposables);

                ModlistLocation.WhenAnyValue(vm => vm.TargetPath)
                    .Subscribe(async p => {
                        if (string.IsNullOrEmpty(Settings.ModListName))
                        {
                            Settings = new CompilerSettingsVM(await InferModListFromLocation(p));
                        }
                        else if(p.FileName == "modlist.txt".ToRelativePath()) await ReInferSettings(p);
                    })
                    .DisposeWith(disposables);

                this.WhenAnyValue(x => x.DownloadLocation.TargetPath)
                    .CombineLatest(this.WhenAnyValue(x => x.ModlistLocation.TargetPath),
                        this.WhenAnyValue(x => x.OutputLocation.TargetPath),
                        this.WhenAnyValue(x => x.DownloadLocation.ErrorState),
                        this.WhenAnyValue(x => x.ModlistLocation.ErrorState),
                        this.WhenAnyValue(x => x.OutputLocation.ErrorState))
                    .Select(_ => Validate())
                    .BindToStrict(this, vm => vm.ErrorState)
                    .DisposeWith(disposables);

                this.WhenAnyValue(x => x.Settings)
                    .Throttle(TimeSpan.FromSeconds(2))
                    .Subscribe(_ => SaveSettingsFile().FireAndForget())
                    .DisposeWith(disposables);
                /*

                ModListImageLocation.WhenAnyValue(x => x.TargetPath)
                                    .BindToStrict(this, vm => vm.Settings.ModListImage)
                                    .DisposeWith(disposables);

                DownloadLocation.WhenAnyValue(x => x.TargetPath)
                                .BindToStrict(this, vm => vm.Settings.Downloads)
                                .DisposeWith(disposables);

                Settings.WhenAnyValue(x => x.Downloads)
                        .BindToStrict(this, vm => vm.DownloadLocation.TargetPath)
                        .DisposeWith(disposables);
                */

            });
        }



        private async Task ReInferSettings(AbsolutePath filePath)
        {
            var newSettings = await _inferencer.InferModListFromLocation(filePath);

            if (newSettings == null)
            {
                _logger.LogError("Cannot infer settings");
                return;
            }

            Settings.Source = newSettings.Source;
            Settings.Downloads = newSettings.Downloads;

            if (string.IsNullOrEmpty(Settings.ModListName))
                Settings.OutputFile = newSettings.OutputFile.Combine(newSettings.Profile).WithExtension(Ext.Wabbajack);
            else
                Settings.OutputFile = newSettings.OutputFile.Combine(newSettings.ModListName).WithExtension(Ext.Wabbajack);

            Settings.Game = newSettings.Game;
            Settings.Include = newSettings.Include;
            Settings.Ignore = newSettings.Ignore;
            Settings.AlwaysEnabled = newSettings.AlwaysEnabled;
            Settings.NoMatchInclude = newSettings.NoMatchInclude;
            Settings.AdditionalProfiles = newSettings.AdditionalProfiles;
        }

        private ErrorResponse Validate()
        {
            var errors = new List<ErrorResponse>
            {
                DownloadLocation.ErrorState,
                ModlistLocation.ErrorState,
                OutputLocation.ErrorState
            };
            return ErrorResponse.Combine(errors);
        }

        private async Task<CompilerSettings> InferModListFromLocation(AbsolutePath path)
        {
            using var _ = LoadingLock.WithLoading();

            CompilerSettings settings;
            if (path == default) return new();
            if (path.FileName.Extension == Ext.CompilerSettings)
            {
                await using var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                settings = (await _dtos.DeserializeAsync<CompilerSettings>(fs))!;
            }
            else if (path.FileName == "modlist.txt".ToRelativePath())
            {
                settings = await _inferencer.InferModListFromLocation(path);
                if (settings == null) return new();
            }
            else
            {
                return new();
            }

            return settings;
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

                    Settings.UseGamePaths = true;
                    if (Settings.OutputFile.DirectoryExists())
                        Settings.OutputFile = Settings.OutputFile.Combine(Settings.ModListName.ToRelativePath()
                            .WithExtension(Ext.Wabbajack));

                    if (Settings.PublishUpdate && !await RunPreflightChecks(token))
                    {
                        State = CompilerState.Errored;
                        return;
                    }

                    var compiler = MO2Compiler.Create(_serviceProvider, Settings.ToCompilerSettings());

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

                    if (Settings.PublishUpdate)
                    {
                        _logger.LogInformation("Publishing List");
                        var downloadMetadata = _dtos.Deserialize<DownloadMetadata>(
                            await Settings.OutputFile.WithExtension(Ext.Meta).WithExtension(Ext.Json).ReadAllTextAsync())!;
                        await _wjClient.PublishModlist(Settings.MachineUrl, Version.Parse(Settings.Version), Settings.OutputFile, downloadMetadata);
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
                        _logger.LogInformation(ex, "Failed Compilation : {Message}", ex.Message);
                        return Disposable.Empty;
                    });
                }
            });

            await tsk;
        }

        private async Task<bool> RunPreflightChecks(CancellationToken token)
        {
            var lists = await _wjClient.GetMyModlists(token);
            if (!lists.Any(x => x.Equals(Settings.MachineUrl, StringComparison.InvariantCultureIgnoreCase)))
            {
                _logger.LogError("Preflight Check failed, list {MachineUrl} not found in any repository", Settings.MachineUrl);
                return false;
            }

            if(!Version.TryParse(Settings.Version, out var version))
            {
                _logger.LogError("Preflight Check failed, version {Version} was not valid", Settings.Version);
                return false;
            }

            return true;
        }

        private async Task SaveSettingsFile()
        {
            if (Settings.Source == default || Settings.CompilerSettingsPath == default) return;

            await using var st = Settings.CompilerSettingsPath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(st, Settings.ToCompilerSettings(), _dtos.Options);

            var allSavedCompilerSettings = await _settingsManager.Load<List<AbsolutePath>>(Consts.AllSavedCompilerSettingsPaths);

            // Don't simply remove Settings.CompilerSettingsPath here, because WJ sometimes likes to make default compiler settings files
            allSavedCompilerSettings.RemoveAll(path => path.Parent == Settings.Source);
            allSavedCompilerSettings.Insert(0, Settings.CompilerSettingsPath);

            await _settingsManager.Save(Consts.AllSavedCompilerSettingsPaths, allSavedCompilerSettings);
        }

        private async Task LoadLastSavedSettings()
        {
            AbsolutePath lastPath = default;
            var allSavedCompilerSettings = await _settingsManager.Load<List<AbsolutePath>>(Consts.AllSavedCompilerSettingsPaths);
            if (allSavedCompilerSettings.Any())
                lastPath = allSavedCompilerSettings[0];

            if (lastPath == default || !lastPath.FileExists() || lastPath.FileName.Extension != Ext.CompilerSettings) return;
            ModlistLocation.TargetPath = lastPath;
        }

        #region ListOps

        public void AddOtherProfile(string profile)
        {
            Settings.AdditionalProfiles = (Settings.AdditionalProfiles ?? Array.Empty<string>()).Append(profile).Distinct().ToArray();
        }

        public void RemoveProfile(string profile)
        {
            Settings.AdditionalProfiles = Settings.AdditionalProfiles.Where(p => p != profile).ToArray();
        }
        
        public void AddAlwaysEnabled(RelativePath path)
        {
            Settings.AlwaysEnabled = (Settings.AlwaysEnabled ?? Array.Empty<RelativePath>()).Append(path).Distinct().ToArray();
        }

        public void RemoveAlwaysEnabled(RelativePath path)
        {
            Settings.AlwaysEnabled = Settings.AlwaysEnabled.Where(p => p != path).ToArray();
        }
        
        public void AddNoMatchInclude(RelativePath path)
        {
            Settings.NoMatchInclude = (Settings.NoMatchInclude ?? Array.Empty<RelativePath>()).Append(path).Distinct().ToArray();
        }

        public void RemoveNoMatchInclude(RelativePath path)
        {
            Settings.NoMatchInclude = Settings.NoMatchInclude.Where(p => p != path).ToArray();
        }
        
        public void AddInclude(RelativePath path)
        {
            Settings.Include = (Settings.Include ?? Array.Empty<RelativePath>()).Append(path).Distinct().ToArray();
        }

        public void RemoveInclude(RelativePath path)
        {
            Settings.Include = Settings.Include.Where(p => p != path).ToArray();
        }

        
        public void AddIgnore(RelativePath path)
        {
            Settings.Ignore = (Settings.Ignore ?? Array.Empty<RelativePath>()).Append(path).Distinct().ToArray();
        }

        public void RemoveIgnore(RelativePath path)
        {
            Settings.Ignore = Settings.Ignore.Where(p => p != path).ToArray();
        }

        #endregion
    }
}
