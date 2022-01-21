using System;
using System.Threading;
using Microsoft.AspNetCore.Components;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.App.Blazor.Utility;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Services.OSIntegrated;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Wabbajack.App.Blazor.Models;
using Wabbajack.App.Blazor.State;

namespace Wabbajack.App.Blazor.Pages;

public partial class Configure
{
    [Inject] private ILogger<Configure> Logger { get; set; } = default!;
    [Inject] private IStateContainer StateContainer { get; set; } = default!;
    [Inject] private DTOSerializer DTOs { get; set; } = default!;
    [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;
    [Inject] private SystemParametersConstructor ParametersConstructor { get; set; } = default!;
    [Inject] private IGameLocator GameLocator { get; set; } = default!;
    [Inject] private SettingsManager SettingsManager { get; set; } = default!;
    [Inject] private LoggerProvider LoggerProvider { get; set; } = default!;
    [Inject] private JSRuntime JSRuntime { get; set; } = default!;
    
    private ModList? Modlist => StateContainer.Modlist;

    private AbsolutePath ModlistPath => StateContainer.ModlistPath;
    private AbsolutePath InstallPath { get; set; }
    private AbsolutePath DownloadPath { get; set; }

    private string StatusText { get; set; } = string.Empty;
    private InstallState InstallState => StateContainer.InstallState;
    // private LoggerProvider.ILogMessage CurrentLog { get; set; }

    private const string InstallSettingsPrefix = "install-settings-";

    private bool _shouldRender;
    protected override bool ShouldRender() => _shouldRender;

    protected override async Task OnInitializedAsync()
    {
        // var Location = KnownFolders.EntryPoint.Combine("downloaded_mod_lists", machineURL).WithExtension(Ext.Wabbajack);
        
        await CheckValidInstallPath();
        _shouldRender = true;
    }

    private async Task CheckValidInstallPath()
    {
        if (ModlistPath == AbsolutePath.Empty) return;
        
        var modlist = await StandardInstaller.LoadFromFile(DTOs, ModlistPath);
        StateContainer.Modlist = modlist;

        var hex = (await ModlistPath.ToString().Hash()).ToHex();
        var prevSettings = await SettingsManager.Load<SavedInstallSettings>(InstallSettingsPrefix + hex);

        if (prevSettings.ModlistLocation == ModlistPath)
        {
            StateContainer.ModlistPath = prevSettings.ModlistLocation;
            InstallPath  = prevSettings.InstallLocation;
            DownloadPath = prevSettings.DownloadLocation;
            //ModlistMetadata = metadata ?? prevSettings.Metadata;
        }

        // see https://docs.microsoft.com/en-us/aspnet/core/blazor/images?view=aspnetcore-6.0#streaming-examples
        var imageStream = await StandardInstaller.ModListImageStream(ModlistPath);
        var dotnetImageStream = new DotNetStreamReference(imageStream);
        // setImageUsingStreaming accepts the img id and the data stream
        await JSRuntime.InvokeVoidAsync("setImageUsingStreaming", "background-image", dotnetImageStream);
    }

    private async void SelectInstallFolder()
    {
        try
        {
            var installPath = await Dialog.ShowDialogNonBlocking(true);
            if (installPath is not null) InstallPath = (AbsolutePath)installPath;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Exception selecting install folder");
        }
    }

    private async void SelectDownloadFolder()
    {
        try
        {
            var downloadPath = await Dialog.ShowDialogNonBlocking(true);
            if (downloadPath is not null) DownloadPath = (AbsolutePath)downloadPath;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Exception selecting download folder");
        }
    }

    private async Task Install()
    {
        if (Modlist is null) return;
        
        StateContainer.InstallState = InstallState.Installing;
        await Task.Run(() => BeginInstall(Modlist));
    }

    private async Task BeginInstall(ModList modlist)
    {
        var postfix = (await ModlistPath.ToString().Hash()).ToHex();
        await SettingsManager.Save(InstallSettingsPrefix + postfix, new SavedInstallSettings
        {
            ModlistLocation = ModlistPath,
            InstallLocation = InstallPath,
            DownloadLocation = DownloadPath
        });

        try
        {
            var installer = StandardInstaller.Create(ServiceProvider, new InstallerConfiguration
            {
                Game = modlist.GameType,
                Downloads = DownloadPath,
                Install = InstallPath,
                ModList = modlist,
                ModlistArchive = ModlistPath,
                SystemParameters = ParametersConstructor.Create(),
                GameFolder = GameLocator.GameLocation(modlist.GameType)
            });
            
            installer.OnStatusUpdate = update =>
            {
                var (statusText, _, _) = update;
                if (StatusText == statusText) return;
                StatusText = statusText;
            };

            await installer.Begin(CancellationToken.None);
            StateContainer.InstallState = InstallState.Success;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Exception installing Modlist");
            StateContainer.InstallState = InstallState.Failure;
        }
    }
}

internal class SavedInstallSettings
{
    public AbsolutePath ModlistLocation { get; set; } = AbsolutePath.Empty;
    public AbsolutePath InstallLocation { get; set; } = AbsolutePath.Empty;
    public AbsolutePath DownloadLocation { get; set; } = AbsolutePath.Empty;
    // public ModlistMetadata Metadata { get; set; }
}
