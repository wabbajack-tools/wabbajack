using System;
using System.Diagnostics;
using System.IO;
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
using Wabbajack.App.Blazor.Models;
using Wabbajack.App.Blazor.State;

namespace Wabbajack.App.Blazor.Pages;

public partial class Configure
{
    [Inject] private NavigationManager           NavigationManager      { get; set; }
    [Inject] private GlobalState                 GlobalState            { get; set; }
    [Inject] private DTOSerializer               _dtos                  { get; set; }
    [Inject] private IServiceProvider            _serviceProvider       { get; set; }
    [Inject] private SystemParametersConstructor _parametersConstructor { get; set; }
    [Inject] private IGameLocator                _gameLocator           { get; set; }
    [Inject] private SettingsManager             _settingsManager       { get; set; }
    [Inject] private LoggerProvider              _loggerProvider        { get; set; }

    private string       Image        { get; set; }
    private ModList      ModList      { get; set; } = new(); // Init a new modlist so we can listen for changes in Blazor components.
    private AbsolutePath ModListPath  { get; set; }
    private AbsolutePath InstallPath  { get; set; }
    private AbsolutePath DownloadPath { get; set; }

    private string                       StatusText   { get; set; }
    public  GlobalState.InstallStateEnum InstallState { get; set; }
    private LoggerProvider.ILogMessage   CurrentLog   { get; set; }

    private const string InstallSettingsPrefix = "install-settings-";

    protected override async Task OnInitializedAsync()
    {
        // var Location = KnownFolders.EntryPoint.Combine("downloaded_mod_lists", machineURL).WithExtension(Ext.Wabbajack);
        GlobalState.OnInstallStateChange += () => InstallState = GlobalState.InstallState;
        await CheckValidInstallPath();
        await base.OnInitializedAsync();
    }

    private async Task CheckValidInstallPath()
    {
        if (GlobalState.ModListPath == AbsolutePath.Empty) return;

        ModListPath         = GlobalState.ModListPath;
        ModList             = await StandardInstaller.LoadFromFile(_dtos, ModListPath);
        GlobalState.ModList = ModList;

        string hex = (await ModListPath.ToString().Hash()).ToHex();
        var prevSettings = await _settingsManager.Load<SavedInstallSettings>(InstallSettingsPrefix + hex);

        if (prevSettings.ModListLocation == ModListPath)
        {
            ModListPath  = prevSettings.ModListLocation;
            InstallPath  = prevSettings.InstallLocation;
            DownloadPath = prevSettings.DownloadLoadction;
            //ModlistMetadata = metadata ?? prevSettings.Metadata;
        }

        Stream image = await StandardInstaller.ModListImageStream(ModListPath);
        await using var reader = new MemoryStream();
        await image.CopyToAsync(reader);
        Image = $"data:image/png;base64,{Convert.ToBase64String(reader.ToArray())}";
    }

    private async void SelectInstallFolder()
    {
        try
        {
            AbsolutePath? thing = await Dialog.ShowDialogNonBlocking(true);
            if (thing != null) InstallPath = (AbsolutePath)thing;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Debug.Print(ex.Message);
        }
    }

    private async void SelectDownloadFolder()
    {
        try
        {
            AbsolutePath? thing = await Dialog.ShowDialogNonBlocking(true);
            if (thing != null) DownloadPath = (AbsolutePath)thing;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Debug.Print(ex.Message);
        }
    }

    private async Task Install()
    {
        GlobalState.InstallState = GlobalState.InstallStateEnum.Installing;
        await Task.Run(BeginInstall);
    }

    private async Task BeginInstall()
    {
        string postfix = (await ModListPath.ToString().Hash()).ToHex();
        await _settingsManager.Save(InstallSettingsPrefix + postfix, new SavedInstallSettings
        {
            ModListLocation   = ModListPath,
            InstallLocation   = InstallPath,
            DownloadLoadction = DownloadPath
        });

        try
        {
            var installer = StandardInstaller.Create(_serviceProvider, new InstallerConfiguration
            {
                Game             = ModList.GameType,
                Downloads        = DownloadPath,
                Install          = InstallPath,
                ModList          = ModList,
                ModlistArchive   = ModListPath,
                SystemParameters = _parametersConstructor.Create(),
                GameFolder       = _gameLocator.GameLocation(ModList.GameType)
            });


            installer.OnStatusUpdate = update =>
            {
                if (StatusText != update.StatusText)
                {
                    StatusText = update.StatusText;
                    InvokeAsync(StateHasChanged);
                }
            };

            await installer.Begin(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.Print(ex.Message);
        }
    }
}

internal class SavedInstallSettings
{
    public AbsolutePath    ModListLocation   { get; set; }
    public AbsolutePath    InstallLocation   { get; set; }
    public AbsolutePath    DownloadLoadction { get; set; }
    public ModlistMetadata Metadata          { get; set; }
}
