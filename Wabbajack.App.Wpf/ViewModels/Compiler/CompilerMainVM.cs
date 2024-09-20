using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Microsoft.Extensions.Logging;
using Wabbajack.Messages;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using System.Windows.Input;

namespace Wabbajack
{
    public class CompilerMainVM : BaseCompilerVM, ICpuStatusVM
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly CompilerSettingsInferencer _inferencer;
        
        [Reactive] public string StatusText { get; set; }
        [Reactive] public Percent StatusProgress { get; set; }

        [Reactive]
        public CompilerState State { get; set; }
        
        [Reactive]
        public MO2CompilerVM SubCompilerVM { get; set; }
        
        public FilePickerVM OutputLocation { get; private set; }
        
        public ReactiveCommand<Unit, Unit> NextCommand { get; }

        public LogStream LoggerProvider { get; }
        public ReadOnlyObservableCollection<CPUDisplayVM> StatusList => _resourceMonitor.Tasks;
        
        [Reactive]
        public ErrorResponse ErrorState { get; private set; }

        public ICommand PrevCommand { get; }
        public ICommand StartCommand { get; }
        
        public CompilerMainVM(ILogger<CompilerMainVM> logger, DTOSerializer dtos, SettingsManager settingsManager,
            IServiceProvider serviceProvider, LogStream loggerProvider, ResourceMonitor resourceMonitor, 
            CompilerSettingsInferencer inferencer, Client wjClient) : base(dtos, settingsManager, logger, wjClient)
        {
            _serviceProvider = serviceProvider;
            LoggerProvider = loggerProvider;
            _resourceMonitor = resourceMonitor;
            _inferencer = inferencer;
            PrevCommand = ReactiveCommand.Create(PrevPage);
            StartCommand = ReactiveCommand.Create(StartCompilation);

            StatusProgress = Percent.Zero;

            BackCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await SaveSettings();
                NavigateToGlobal.Send(ScreenType.Home);
            });

            SubCompilerVM = new MO2CompilerVM(this);

            NextCommand = ReactiveCommand.CreateFromTask(NextPage);

            
            this.WhenActivated(disposables =>
            {
                State = CompilerState.Configuration;

                OutputLocation = new FilePickerVM
                {
                    ExistCheckOption = FilePickerVM.CheckOptions.Off,
                    PathType = FilePickerVM.PathTypeOptions.Folder,
                    PromptTitle = "Location where the compiled modlist will be saved to"
                };

                Disposable.Empty.DisposeWith(disposables);
            });
        }

        private void PrevPage()
        {
            NavigateToGlobal.Send(ScreenType.CompilerFileManager);
            LoadCompilerSettings.Send(Settings.ToCompilerSettings());
        }

        private void Click()
        {
            int i = 0;
            //Settings.ModListImage = (AbsolutePath)@"C:\Users\tik\Downloads\1.0.0.png";
        }

        private async Task ReInferSettings(AbsolutePath filePath)
        {
            var newSettings = await _inferencer.InferModListFromLocation(filePath);

            if (newSettings == null)
            {
                _logger.LogError("Cannot infer settings from {0}", filePath);
                return;
            }

            Settings.Source = newSettings.Source;
            Settings.Downloads = newSettings.Downloads;

            if (string.IsNullOrEmpty(Settings.ModListName))
                Settings.OutputFile = newSettings.OutputFile.Combine(newSettings.Profile).WithExtension(Ext.Wabbajack);
            else
                Settings.OutputFile = newSettings.OutputFile.Combine(newSettings.ModListName).WithExtension(Ext.Wabbajack);

            Settings.Game = newSettings.Game;
            Settings.Include = newSettings.Include.ToHashSet();
            Settings.Ignore = newSettings.Ignore.ToHashSet();
            Settings.AlwaysEnabled = newSettings.AlwaysEnabled.ToHashSet();
            Settings.NoMatchInclude = newSettings.NoMatchInclude.ToHashSet();
            Settings.AdditionalProfiles = newSettings.AdditionalProfiles;
        }

        private ErrorResponse Validate()
        {
            var errors = new List<ErrorResponse>
            {
                OutputLocation.ErrorState
            };
            return ErrorResponse.Combine(errors);
        }

        private async Task NextPage()
        {
            await SaveSettings();
            //NavigateToGlobal.Send(ScreenType.CompilerFileManager);
            //LoadCompilerSettings.Send(Settings.ToCompilerSettings());
            await StartCompilation();
        }

        private async Task StartCompilation()
        {
            var tsk = Task.Run(async () =>
            {
                try
                {
                    await SaveSettings();
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
    }
}
