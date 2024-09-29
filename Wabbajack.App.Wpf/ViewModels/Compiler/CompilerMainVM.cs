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

namespace Wabbajack;

public class CompilerMainVM : BaseCompilerVM, IHasInfoVM, ICpuStatusVM
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ResourceMonitor _resourceMonitor;

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
        CompilerDetailsVM compilerDetailsVM, CompilerFileManagerVM compilerFileManagerVM) : base(dtos, settingsManager, logger, wjClient)
    {
        _serviceProvider = serviceProvider;
        _resourceMonitor = resourceMonitor;

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
                ConfigurationText = "Modlist Details";
                ProgressText = "Compilation";
                ProgressPercent = Percent.Zero;
                CurrentStep = Step.Configuration;
                State = CompilerState.Configuration;
                ProgressState = ProgressState.Normal;
            }

            Disposable.Empty.DisposeWith(disposables);
        });
    }

    private void OpenLog()
    {
        var log = KnownFolders.LauncherAwarePath.Combine("logs").Combine("Wabbajack.current.log").ToString();
        Process.Start(new ProcessStartInfo(log) { UseShellExecute = true });
    }

    private void Publish()
    {
        throw new NotImplementedException();
    }

    private void OpenFolder() => Process.Start(new ProcessStartInfo() { FileName = "explorer.exe ", Arguments = $"/select, \"{Settings.OutputFile}\"" });

    private void Info() => Process.Start(new ProcessStartInfo("https://wiki.wabbajack.org/modlist_author_documentation/Compilation.html") { UseShellExecute = true });

    private async Task StartCompilation()
    {
        var tsk = Task.Run((Func<Task>)(async () =>
        {
            try
            {
                await SaveSettings();
                var token = CancellationTokenSource.Token;
                RxApp.MainThreadScheduler.Schedule(_logger, (Func<System.Reactive.Concurrency.IScheduler, ILogger<BaseCompilerVM>, IDisposable>)((_, _) =>
                {
                    this.ProgressText = "Compiling...";
                    State = CompilerState.Compiling;
                    CurrentStep = Step.Busy;
                    ProgressText = "Compiling...";
                    ProgressState = ProgressState.Normal;
                    return Disposable.Empty;
                }));

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
                    .Subscribe((Action<EventPattern<StatusUpdate>>)(update =>
                    {
                        var s = update.EventArgs;
                        ProgressText = $"Step {s.CurrentStep} - {s.StatusText}";
                        ProgressPercent = s.StepsProgress;
                    }));


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

                RxApp.MainThreadScheduler.Schedule(_logger, (Func<System.Reactive.Concurrency.IScheduler, ILogger<BaseCompilerVM>, IDisposable>)((_, _) =>
                {
                    ProgressText = "Compiled";
                    ProgressPercent = Percent.One;
                    State = CompilerState.Completed;
                    CurrentStep = Step.Done;
                    ProgressState = ProgressState.Success;
                    return Disposable.Empty;
                }));


            }
            catch (Exception ex)
            {
                RxApp.MainThreadScheduler.Schedule(_logger, (Func<System.Reactive.Concurrency.IScheduler, ILogger<BaseCompilerVM>, IDisposable>)((_, _) =>
                {
                    if (Cancelling)
                    {
                        this.ProgressText = "Compilation Cancelled";
                        ProgressPercent = Percent.Zero;
                        State = CompilerState.Configuration;
                        _logger.LogInformation(ex, "Cancelled Compilation : {Message}", ex.Message);
                        Cancelling = false;
                        return Disposable.Empty;
                    }
                    else
                    {
                        this.ProgressText = "Compilation Failed";
                        ProgressPercent = Percent.Zero;

                        State = CompilerState.Errored;
                        _logger.LogInformation(ex, "Failed Compilation : {Message}", ex.Message);
                        return Disposable.Empty;
                    }
                }));
            }
        }));

        await tsk;
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
