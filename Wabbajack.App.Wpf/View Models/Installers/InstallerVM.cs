using System;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Media.Imaging;
using ReactiveUI.Fody.Helpers;
using System.Windows.Media;
using DynamicData;
using DynamicData.Binding;
using System.Reactive;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Shell;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Extensions;
using Wabbajack.Installer;
using Wabbajack.Interventions;
using Wabbajack.Messages;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.View_Models;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Consts = Wabbajack.Consts;
using KnownFolders = Wabbajack.Paths.IO.KnownFolders;

namespace Wabbajack;

public enum ModManager
{
    Standard
}

public class InstallerVM : BackNavigatingVM, IBackNavigatingVM
{
    private const string LastLoadedModlist = "last-loaded-modlist";
    
    [Reactive]
    public ModList ModList { get; set; }
    
    [Reactive]
    public ErrorResponse? Completed { get; set; }

    [Reactive]
    public FilePickerVM ModListLocation { get; set; }
    
    [Reactive]
    public MO2InstallerVM Installer { get; set; }
    
    [Reactive]
    public BitmapFrame ModListImage { get; set; }
    
    [Reactive]
    
    public BitmapFrame SlideShowImage { get; set; }
    
    
    /// <summary>
    ///  Slideshow Data
    /// </summary>
    [Reactive]
    public string SlideShowTitle { get; set; }
    
    [Reactive]
    public string SlideShowAuthor { get; set; }
    
    [Reactive]
    public string SlideShowDescription { get; set; }


    private readonly ObservableAsPropertyHelper<bool> _installing;
    private readonly DTOSerializer _dtos;
    private readonly ILogger<InstallerVM> _logger;
    private readonly SettingsManager _settingsManager;

    [Reactive]
    public bool Installing { get; set; }
    
    
    // Command properties
    public ReactiveCommand<Unit, Unit> ShowManifestCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenReadmeCommand { get; }
    public ReactiveCommand<Unit, Unit> VisitModListWebsiteCommand { get; }
        
    public ReactiveCommand<Unit, Unit> CloseWhenCompleteCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenLogsCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToInstallCommand { get; }
    public ReactiveCommand<Unit, Unit> BeginCommand { get; }
    
    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    public InstallerVM(ILogger<InstallerVM> logger, DTOSerializer dtos, SettingsManager settingsManager) : base(logger)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _dtos = dtos;
        Installer = new MO2InstallerVM(this);
        
        BackCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.ModeSelectionView));
        
        OpenReadmeCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(new Uri(ModList!.Readme));
        }, LoadingLock.IsNotLoadingObservable);
        
        VisitModListWebsiteCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(ModList!.Website);
        }, LoadingLock.IsNotLoadingObservable);
        
        ModListLocation = new FilePickerVM
        {
            ExistCheckOption = FilePickerVM.CheckOptions.On,
            PathType = FilePickerVM.PathTypeOptions.File,
            PromptTitle = "Select a ModList to install"
        };
        ModListLocation.Filters.Add(new CommonFileDialogFilter("Wabbajack Modlist", "*.wabbajack"));
        
        MessageBus.Current.Listen<LoadModlistForInstalling>()
            .Subscribe(msg => LoadModlist(msg.Path).FireAndForget())
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<LoadLastLoadedModlist>()
            .Subscribe(msg =>
            {
                LoadLastModlist().FireAndForget();
            });

        this.WhenActivated(disposables =>
        {
            ModListLocation.WhenAnyValue(l => l.TargetPath)
                .Subscribe(p => LoadModlist(p).FireAndForget())
                .DisposeWith(disposables);

        });

    }

    private async Task LoadLastModlist()
    {
        var lst = await _settingsManager.Load<AbsolutePath>(LastLoadedModlist);
        if (lst.FileExists())
            await LoadModlist(lst);
    }

    private async Task LoadModlist(AbsolutePath path)
    {
        using var ll = LoadingLock.WithLoading();
        ModListLocation.TargetPath = path;
        try
        {
            ModList = await StandardInstaller.LoadFromFile(_dtos, path);
            ModListImage = BitmapFrame.Create(await StandardInstaller.ModListImageStream(path));
            PopulateSlideShow(ModList);
            
            ll.Succeed();
            await _settingsManager.Save(LastLoadedModlist, path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While loading modlist");
            ll.Fail();
        }
    }

    private void PopulateSlideShow(ModList modList)
    {
        SlideShowTitle = modList.Name;
        SlideShowAuthor = modList.Author;
        SlideShowDescription = modList.Description;
        SlideShowImage = ModListImage;
    }

}