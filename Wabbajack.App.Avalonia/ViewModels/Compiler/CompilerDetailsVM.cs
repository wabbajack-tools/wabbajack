using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.ViewModels.Common;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.ViewModels.Compiler;

public enum CompilerState
{
    Configuration,
    Compiling,
    Completed,
    Errored
}

/// <summary>
/// The form on the compiler screen: the list's name, author, version, description, image, folders, links and
/// profiles. Its settings are the ones the compiler runs with; the pickers are made anew each time it is shown.
/// </summary>
public partial class CompilerDetailsVM : BaseCompilerVM, ICpuStatusVM
{
    private readonly ResourceMonitor _resourceMonitor;
    private readonly CompilerSettingsInferencer _inferencer;
    private readonly Navigator _navigator;

    public CompilerDetailsVM(ILogger<CompilerDetailsVM> logger, DTOSerializer dtos, SettingsManager settingsManager,
        LogStream loggerProvider, ResourceMonitor resourceMonitor, CompilerSettingsInferencer inferencer, Client wjClient,
        CompilerFileManagerVM compilerFileManagerVM, Navigator navigator) : base(dtos, settingsManager, logger, wjClient)
    {
        LoggerProvider = loggerProvider;
        _resourceMonitor = resourceMonitor;
        _inferencer = inferencer;
        _navigator = navigator;
        CompilerFileManagerVM = compilerFileManagerVM;

        // WPF's own Start command, which nothing on screen is bound to: the Compile List button runs
        // CompilerMainVM's.
        StartCommand = ReactiveCommand.CreateFromTask(StartCompilation);

        this.WhenActivated(disposables =>
        {
            State = CompilerState.Configuration;

            var modlistLocation = new FilePickerVM
            {
                ExistCheckOption = FilePickerVM.CheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.File,
                PromptTitle = "Select a config file or a modlist.txt file",
                TargetPath = Settings.ProfilePath
            };
            modlistLocation.Filters.AddRange(new[]
            {
                new CommonFileDialogFilter("MO2 Modlist", "*" + Ext.Txt),
                new CommonFileDialogFilter("Compiler Settings File", "*" + Ext.CompilerSettings)
            });
            ModlistLocation = modlistLocation;

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
                PromptTitle = "Location where the compiled modlist will be stored",
                PathTransformer = folder => folder.DirectoryExists()
                    ? folder.Combine(!string.IsNullOrWhiteSpace(Settings?.ModListName) ? Settings.ModListName : "Default")
                        .WithExtension(Ext.Wabbajack)
                    : folder
            };

            var imageLocation = new FilePickerVM
            {
                ExistCheckOption = FilePickerVM.CheckOptions.On,
                PathType = FilePickerVM.PathTypeOptions.File,
                PromptTitle = "Thumbnail image file to use for the modlist"
            };
            imageLocation.Filters.AddRange(new[]
            {
                new CommonFileDialogFilter("WebP Image (preferred)", "*" + Ext.Webp),
                new CommonFileDialogFilter("PNG Image", "*" + Ext.Png),
                new CommonFileDialogFilter("JPG Image", "*" + Ext.Jpg)
            });
            ModListImageLocation = imageLocation;

            ModlistLocation.WhenAnyValue(vm => vm.TargetPath)
                .Subscribe(async p =>
                {
                    if (p == default) return;
                    if (Settings.CompilerSettingsPath != default) return;
                    if (p.FileName == "modlist.txt".ToRelativePath()) await ReInferSettings(p);
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.DownloadLocation.TargetPath)
                .CombineLatest(this.WhenAnyValue(x => x.ModlistLocation.TargetPath),
                    this.WhenAnyValue(x => x.OutputLocation.TargetPath),
                    this.WhenAnyValue(x => x.DownloadLocation.ValidationResult),
                    this.WhenAnyValue(x => x.ModlistLocation.ValidationResult),
                    this.WhenAnyValue(x => x.OutputLocation.ValidationResult))
                .Select(_ => Validate())
                .Subscribe(v => ErrorState = v)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.Settings.Source)
                .Subscribe(source =>
                {
                    AvailableProfiles = source.Combine("profiles").EnumerateDirectories(recursive: false)
                        .Select(dir => dir.FileName.ToString()).ToList();
                })
                .DisposeWith(disposables);
        });
    }

    public CompilerFileManagerVM CompilerFileManagerVM { get; }

    [Reactive] public partial List<string> AvailableProfiles { get; set; }
    [Reactive] public partial CompilerState State { get; set; }

    // Paths
    [Reactive] public partial FilePickerVM ModlistLocation { get; private set; }
    [Reactive] public partial FilePickerVM DownloadLocation { get; private set; }
    [Reactive] public partial FilePickerVM OutputLocation { get; private set; }
    [Reactive] public partial FilePickerVM ModListImageLocation { get; private set; } = new();

    public ReactiveCommand<Unit, Unit> StartCommand { get; }

    public LogStream LoggerProvider { get; }
    public ReadOnlyObservableCollection<CPUDisplayVM> StatusList => _resourceMonitor.Tasks;

    [Reactive] public partial ValidationResult ErrorState { get; private set; }

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

    private ValidationResult Validate()
    {
        var errors = new List<ValidationResult>
        {
            DownloadLocation.ValidationResult,
            ModlistLocation.ValidationResult,
            OutputLocation.ValidationResult
        };
        return ValidationResult.Combine(errors);
    }

    private async Task StartCompilation()
    {
        await SaveSettings();
        _navigator.NavigateTo(ScreenType.CompilerMain);
        LoadCompilerSettings.Send(Settings.ToCompilerSettings());
    }
}
