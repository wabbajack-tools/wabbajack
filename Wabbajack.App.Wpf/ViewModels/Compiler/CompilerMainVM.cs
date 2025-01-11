using Microsoft.Extensions.Logging;
using Wabbajack.Messages;
using ReactiveUI;
using System.Reactive.Disposables;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Models;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;
using System.Windows.Input;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs;
using Wabbajack.Extensions;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.LoginManagers;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.DownloadStates;
using System.Reactive.Concurrency;

namespace Wabbajack;

public class CompilerMainVM : BaseCompilerVM, IHasInfoVM, ICpuStatusVM
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ResourceMonitor _resourceMonitor;
    private readonly IEnumerable<INeedsLogin> _logins;
    private readonly DownloadDispatcher _downloadDispatcher;

    public CompilerDetailsVM CompilerDetailsVM { get; set; }
    public CompilerFileManagerVM CompilerFileManagerVM { get; set; }

    public LogStream LoggerProvider { get; }
    public CancellationTokenSource CancellationTokenSource { get; private set; }

    public ICommand InfoCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenLogCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand PublishCommand { get; }

    [Reactive] public CompilerState State { get; set; }
    public bool Cancelling { get; private set; }

    public ReadOnlyObservableCollection<CPUDisplayVM> StatusList => _resourceMonitor.Tasks;

    public CompilerMainVM(ILogger<CompilerMainVM> logger, DTOSerializer dtos, SettingsManager settingsManager,
        LogStream loggerProvider, Client wjClient, IServiceProvider serviceProvider, ResourceMonitor resourceMonitor,
        CompilerDetailsVM compilerDetailsVM, CompilerFileManagerVM compilerFileManagerVM, IEnumerable<INeedsLogin> logins, DownloadDispatcher downloadDispatcher) : base(dtos, settingsManager, logger, wjClient)
    {
        _serviceProvider = serviceProvider;
        _resourceMonitor = resourceMonitor;
        _logins = logins;
        _downloadDispatcher = downloadDispatcher;

        LoggerProvider = loggerProvider;
        CompilerDetailsVM = compilerDetailsVM;
        CompilerFileManagerVM = compilerFileManagerVM;

        CancellationTokenSource = new CancellationTokenSource();

        InfoCommand = ReactiveCommand.Create(Info);
        StartCommand = ReactiveCommand.Create(StartCompilation);
        CancelCommand = ReactiveCommand.Create(CancelCompilation);
        OpenLogCommand = ReactiveCommand.Create(OpenLog);
        OpenFolderCommand = ReactiveCommand.Create(OpenFolder);
        PublishCommand = ReactiveCommand.Create(Publish); 

        ProgressPercent = Percent.Zero;
        this.WhenActivated(disposables =>
        {
            if (State != CompilerState.Compiling)
            {
                ShowNavigation.Send();
                ConfigurationText = "Modlist Details";
                ProgressText = "Compilation";
                ProgressPercent = Percent.Zero;
                CurrentStep = Step.Configuration;
                State = CompilerState.Configuration;
                ProgressState = ProgressState.Normal;
            }

            this.WhenAnyValue(x => x.CompilerDetailsVM.Settings)
                .BindTo(this, x => x.Settings)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.CompilerFileManagerVM.Settings.Include)
                .BindTo(this, x => x.Settings.Include)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.CompilerFileManagerVM.Settings.Ignore)
                .BindTo(this, x => x.Settings.Ignore)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.CompilerFileManagerVM.Settings.NoMatchInclude)
                .BindTo(this, x => x.Settings.NoMatchInclude)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.CompilerFileManagerVM.Settings.AlwaysEnabled)
                .BindTo(this, x => x.Settings.AlwaysEnabled)
                .DisposeWith(disposables);
        });
    }

    private void OpenLog()
    {
        var log = KnownFolders.LauncherAwarePath.Combine("logs").Combine("Wabbajack.current.log").ToString();
        Process.Start(new ProcessStartInfo(log) { UseShellExecute = true });
    }

    private async Task Publish()
    {
        bool readyForPublish = await RunPreflightChecks(CancellationToken.None);
        if (!readyForPublish) return;

        _logger.LogInformation("Publishing List");
        var downloadMetadata = _dtos.Deserialize<DownloadMetadata>(
            await Settings.OutputFile.WithExtension(Ext.Meta).WithExtension(Ext.Json).ReadAllTextAsync())!;
        await _wjClient.PublishModlist(Settings.MachineUrl, Version.Parse(Settings.Version), Settings.OutputFile, downloadMetadata);
    }

    private void OpenFolder() => UIUtils.OpenFolderAndSelectFile(Settings.OutputFile);

    private void Info() => Process.Start(new ProcessStartInfo("https://wiki.wabbajack.org/modlist_author_documentation/Compilation.html") { UseShellExecute = true });

    private async Task StartCompilation()
    {
        var tsk = Task.Run(async () =>
        {
            try
            {
                HideNavigation.Send();
                await SaveSettings();
                var token = CancellationTokenSource.Token;

                await EnsureLoggedIntoNexus();

                RxApp.MainThreadScheduler.Schedule(_logger, (_, _) =>
                {
                    ProgressText = "Compiling...";
                    State = CompilerState.Compiling;
                    CurrentStep = Step.Busy;
                    ProgressText = "Compiling...";
                    ProgressState = ProgressState.Normal;
                    return Disposable.Empty;
                });

                Settings.UseGamePaths = true;
                if (Settings.OutputFile.DirectoryExists())
                    RxApp.MainThreadScheduler.Schedule(() => Settings.OutputFile = Settings.OutputFile.Combine(Settings.ModListName.ToRelativePath().WithExtension(Ext.Wabbajack)));

                var compiler = MO2Compiler.Create(_serviceProvider, Settings.ToCompilerSettings());

                var events = Observable.FromEventPattern<StatusUpdate>(h => compiler.OnStatusUpdate += h,
                        h => compiler.OnStatusUpdate -= h)
                    .ObserveOnGuiThread()
                    .Subscribe(update =>
                    {
                        RxApp.MainThreadScheduler.Schedule(_logger, (_, _) =>
                        {
                            var s = update.EventArgs;
                            ProgressText = $"{s.StatusText}";
                            ProgressPercent = s.StepsProgress;
                            return Disposable.Empty;
                        });
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

                _logger.LogInformation("Compiler Finished");

                RxApp.MainThreadScheduler.Schedule(_logger, (_, _) =>
                {
                    ShowNavigation.Send();
                    ProgressText = "Compiled";
                    ProgressPercent = Percent.One;
                    State = CompilerState.Completed;
                    CurrentStep = Step.Done;
                    ProgressState = ProgressState.Success;
                    return Disposable.Empty;
                });


            }
            catch (Exception ex)
            {
                RxApp.MainThreadScheduler.Schedule(_logger, (_, _) =>
                {
                    ShowNavigation.Send();
                    if (Cancelling)
                    {
                        this.ProgressText = "Compilation Cancelled";
                        ProgressPercent = Percent.Zero;
                        State = CompilerState.Configuration;
                        _logger.LogInformation(ex, "Cancelled compilation: {Message}", ex.Message);
                        Cancelling = false;
                        return Disposable.Empty;
                    }
                    else
                    {
                        this.ProgressText = "Compilation Failed";
                        ProgressPercent = Percent.Zero;

                        State = CompilerState.Errored;
                        _logger.LogInformation(ex, "Failed compilation: {Message}", ex.Message);
                        return Disposable.Empty;
                    }
                });
            }
        });

        await tsk;
    }

    private async Task EnsureLoggedIntoNexus()
    {
        var nexusDownloadState = new Nexus();
        foreach (var downloader in await _downloadDispatcher.AllDownloaders([nexusDownloadState]))
        {
            _logger.LogInformation("Preparing {Name}", downloader.GetType().Name);
            if (await downloader.Prepare())
                continue;
            var manager = _logins.FirstOrDefault(l => l.LoginFor() == downloader.GetType());
            if(manager == null)
            {
                _logger.LogError("Cannot install, could not prepare {Name} for downloading", downloader.GetType().Name);
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
    }

    private async Task CancelCompilation()
    {
        if (State != CompilerState.Compiling) return;
        Cancelling = true;
        _logger.LogInformation("Cancel pressed, cancelling compilation...");
        await CancellationTokenSource.CancelAsync();
        CancellationTokenSource = new CancellationTokenSource();
    }

    private async Task<bool> RunPreflightChecks(CancellationToken token)
    {
        var lists = await _wjClient.GetMyModlists(token);
        if (!lists.Any(x => x.Equals(Settings.MachineUrl, StringComparison.InvariantCultureIgnoreCase)))
        {
            _logger.LogError("Preflight Check failed, list {MachineUrl} not found in any repository", Settings.MachineUrl);
            return false;
        }

        if (!Version.TryParse(Settings.Version, out var version))
        {
            _logger.LogError("Preflight Check failed, version {Version} was not valid", Settings.Version);
            return false;
        }

        return true;
    }
}
