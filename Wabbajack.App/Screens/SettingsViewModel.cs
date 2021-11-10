using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.App.Controls;
using Wabbajack.App.Messages;
using Wabbajack.App.Models;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack.App.Screens;

public class SettingsViewModel : ViewModelBase
{
    private readonly Subject<AbsolutePath> _fileSystemEvents = new();
    private readonly ILogger<SettingsViewModel> _logger;
    public readonly IEnumerable<ResourceViewModel> Resources;
    private readonly ResourceSettingsManager _resourceSettingsManager;

    public SettingsViewModel(ILogger<SettingsViewModel> logger, Configuration configuration,
        ResourceSettingsManager resourceSettingsManager,
        NexusApiTokenProvider nexusProvider, IEnumerable<IResource> resources, LoversLabTokenProvider llProvider, VectorPlexusTokenProvider vpProvider)
    {
        _resourceSettingsManager = resourceSettingsManager;
        _logger = logger;
        Resources = resources.Select(r => new ResourceViewModel(r))
            .OrderBy(o => o.Name)
            .ToArray();
        Activator = new ViewModelActivator();

        this.WhenActivated(disposables =>
        {
            foreach (var resource in Resources)
            {
                resource.Activator.Activate().DisposeWith(disposables);
            }

            configuration.EncryptedDataLocation.CreateDirectory();
            Watcher = new FileSystemWatcher(configuration.EncryptedDataLocation.ToString());
            Watcher.DisposeWith(disposables);
            Watcher.Created += Pulse;
            Watcher.Deleted += Pulse;
            Watcher.Renamed += Pulse;
            Watcher.Changed += Pulse;

            Watcher.EnableRaisingEvents = true;

            var haveNexusToken = _fileSystemEvents
                .StartWith(AbsolutePath.Empty)
                .Select(_ => nexusProvider.HaveToken());

            NexusLogin =
                ReactiveCommand.Create(() => { MessageBus.Current.SendMessage(new NavigateTo(typeof(NexusLoginViewModel))); },
                    haveNexusToken.Select(x => !x));
            NexusLogout = ReactiveCommand.Create(nexusProvider.DeleteToken, haveNexusToken.Select(x => x));
            
            var haveLLToken = _fileSystemEvents
                .StartWith(AbsolutePath.Empty)
                .Select(_ => llProvider.HaveToken());

            LoversLabLogin =
                ReactiveCommand.Create(() => { MessageBus.Current.SendMessage(new NavigateTo(typeof(LoversLabOAuthLoginViewModel))); },
                    haveLLToken.Select(x => !x));
            LoversLabLogout = ReactiveCommand.Create(llProvider.DeleteToken, haveLLToken.Select(x => x));
            
            var haveVectorPlexusToken = _fileSystemEvents
                .StartWith(AbsolutePath.Empty)
                .Select(_ => vpProvider.HaveToken());

            VectorPlexusLogin =
                ReactiveCommand.Create(() => { MessageBus.Current.SendMessage(new NavigateTo(typeof(VectorPlexusOAuthLoginViewModel))); },
                    haveVectorPlexusToken.Select(x => !x));
            VectorPlexusLogout = ReactiveCommand.Create(vpProvider.DeleteToken, haveVectorPlexusToken.Select(x => x));
        });
    }

    public ReactiveCommand<Unit, Unit> NexusLogin { get; set; }
    public ReactiveCommand<Unit, Unit> NexusLogout { get; set; }

    
    public ReactiveCommand<Unit, Unit> LoversLabLogin { get; set; }
    public ReactiveCommand<Unit, Unit> LoversLabLogout { get; set; }
    
    public ReactiveCommand<Unit, Unit> VectorPlexusLogin { get; set; }
    public ReactiveCommand<Unit, Unit> VectorPlexusLogout { get; set; }

    public FileSystemWatcher Watcher { get; set; }

    private void Pulse(object sender, FileSystemEventArgs e)
    {
        _fileSystemEvents.OnNext(e.FullPath?.ToAbsolutePath() ?? default);
    }

    public async Task SaveResourceSettingsAndRestart()
    {
        await _resourceSettingsManager.SaveSettings(Resources.ToDictionary(r => r.Name, r =>
            new ResourceSettingsManager.ResourceSetting()
            {
                MaxTasks = r.MaxTasks,
                MaxThroughput = r.MaxThroughput
            }));
        
        var proc = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = Process.GetCurrentProcess().MainModule!.FileName 
            }
        };
        proc.Start();
        Environment.Exit(0);
    }
}