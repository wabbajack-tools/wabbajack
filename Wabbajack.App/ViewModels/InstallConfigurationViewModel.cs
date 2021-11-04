
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Mixins;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Extensions;
using Wabbajack.App.Extensions;
using Wabbajack.App.Messages;
using Wabbajack.App.Models;
using Wabbajack.App.Utilities;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.SavedSettings;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.ViewModels;

public class InstallConfigurationViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly DTOSerializer _dtos;
    private readonly InstallationStateManager _stateManager;
    private readonly SettingsManager _settingsManager;


    public InstallConfigurationViewModel(DTOSerializer dtos, InstallationStateManager stateManager, SettingsManager settingsManager)
    {
        _stateManager = stateManager;
        _settingsManager = settingsManager;

        _dtos = dtos;
        Activator = new ViewModelActivator();

        MessageBus.Current.Listen<StartInstallConfiguration>()
            .Subscribe(Receive)
            .DisposeWith(VMDisposables);
        
        this.WhenActivated(disposables =>
        {
            this.ValidationRule(x => x.ModListPath, p => p.FileExists(), "Wabbajack file must exist");
            this.ValidationRule(x => x.Install, p => p.DirectoryExists(), "Install folder file must exist");
            this.ValidationRule(x => x.Download, p => p != default, "Download folder must be set");

            BeginCommand = ReactiveCommand.Create(() => { StartInstall().FireAndForget(); }, this.IsValid());


            this.WhenAnyValue(t => t.ModListPath)
                .Where(t => t != default)
                .SelectMany(async x => await LoadModList(x))
                .OnUIThread()
                .ObserveOn(AvaloniaScheduler.Instance)
                .BindTo(this, t => t.ModList)
                .DisposeWith(disposables);

            this.WhenAnyValue(t => t.ModListPath)
                .Where(t => t != default)
                .SelectMany(async x => await LoadModListImage(x))
                .OnUIThread()
                .BindTo(this, t => t.ModListImage)
                .DisposeWith(disposables);

            var settings = this.WhenAnyValue(t => t.ModListPath)
                .SelectMany(async v => await _stateManager.Get(v))
                .OnUIThread()
                .Where(s => s != null);

            settings.Select(s => s!.Install)
                .BindTo(this, vm => vm.Install)
                .DisposeWith(disposables);

            settings.Select(s => s!.Downloads)
                .BindTo(this, vm => vm.Download)
                .DisposeWith(disposables);


            LoadSettings().FireAndForget();

        });
    }

    private async Task LoadSettings()
    {
        var path = await _settingsManager.Load<AbsolutePath>("last-install-path");
        if (path != default && path.FileExists())
            ModListPath = path;
    }

    [Reactive] public AbsolutePath ModListPath { get; set; }

    [Reactive] public AbsolutePath Install { get; set; }

    [Reactive] public AbsolutePath Download { get; set; }

    [Reactive] public ModList? ModList { get; set; }

    [Reactive] public IBitmap? ModListImage { get; set; }

    [Reactive] public bool IsReady { get; set; }

    [Reactive] public ReactiveCommand<Unit, Unit> BeginCommand { get; set; }

    public ViewModelActivator Activator { get; }

    private void Receive(StartInstallConfiguration val)
    {
        ModListPath = val.ModList;
    }

    private async Task StartInstall()
    {
        ModlistMetadata? metadata = null;
        var metadataPath = ModListPath.WithExtension(Ext.MetaData);
        if (metadataPath.FileExists())
            metadata = _dtos.Deserialize<ModlistMetadata>(await metadataPath.ReadAllTextAsync());

        await _stateManager.SetLastState(new InstallationConfigurationSetting
        {
            ModList = ModListPath,
            Downloads = Download,
            Install = Install,
            Metadata = metadata
        });

        await _settingsManager.Save("last-install-path", ModListPath);

        MessageBus.Current.SendMessage(new NavigateTo(typeof(StandardInstallationViewModel)));
        MessageBus.Current.SendMessage(new StartInstallation(ModListPath, Install, Download, metadata));
    }

    private async Task<IBitmap> LoadModListImage(AbsolutePath path)
    {
        return new Bitmap(await ModListUtilities.GetModListImageStream(path));
    }

    private async Task<ModList> LoadModList(AbsolutePath modlist)
    {
        var definition = await StandardInstaller.LoadFromFile(_dtos, modlist);
        return definition;
    }
}