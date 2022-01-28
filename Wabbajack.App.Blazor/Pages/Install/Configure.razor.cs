using System;
using System.IO;
using Microsoft.AspNetCore.Components;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.App.Blazor.Utility;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Services.OSIntegrated;
using System.Threading.Tasks;
using Blazored.Toast.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Wabbajack.App.Blazor.State;

namespace Wabbajack.App.Blazor.Pages;

public partial class Configure
{
    [Inject] private ILogger<Configure> Logger { get; set; } = default!;
    [Inject] private IStateContainer StateContainer { get; set; } = default!;
    [Inject] private DTOSerializer DTOs { get; set; } = default!;
    [Inject] private SettingsManager SettingsManager { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IToastService toastService { get; set; }

    private ModList? Modlist => StateContainer.Modlist;
    private string ModlistImage => StateContainer.ModlistImage;
    private AbsolutePath ModlistPath => StateContainer.ModlistPath;
    private AbsolutePath InstallPath => StateContainer.InstallPath;
    private AbsolutePath DownloadPath => StateContainer.DownloadPath;

    private InstallState InstallState => StateContainer.InstallState;

    private const string InstallSettingsPrefix = "install-settings-";

    private bool _shouldRender;
    protected override bool ShouldRender() => _shouldRender;

    protected override async Task OnInitializedAsync()
    {
        await LoadModlist();
        _shouldRender = true;
    }

    private async Task LoadModlist()
    {
        try
        {
            if (ModlistPath == AbsolutePath.Empty) throw new FileNotFoundException("Modlist path was empty.");
            var modlist = await StandardInstaller.LoadFromFile(DTOs, ModlistPath);
            StateContainer.Modlist = modlist;
        }
        catch (Exception e)
        {
            toastService.ShowError("Could not load modlist!");
            Logger.LogError(e, "Exception loading Modlist file {Name}", ModlistPath);
            NavigationManager.NavigateTo(Select.Route);
            return;
        }

        try
        {
            var hex = (await ModlistPath.ToString().Hash()).ToHex();
            var prevSettings = await SettingsManager.Load<SavedInstallSettings>(InstallSettingsPrefix + hex);
            if (prevSettings.ModlistLocation == ModlistPath)
            {
                StateContainer.ModlistPath = prevSettings.ModlistLocation;
                StateContainer.InstallPath = prevSettings.InstallLocation;
                StateContainer.DownloadPath = prevSettings.DownloadLocation;
            }
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Exception loading previous settings for {Name}", ModlistPath);
        }

        try
        {
            var imageStream = await StandardInstaller.ModListImageStream(ModlistPath);
            var dotnetImageStream = new DotNetStreamReference(imageStream);
            StateContainer.ModlistImage = new string(await JSRuntime.InvokeAsync<string>("getBlobUrlFromStream", dotnetImageStream));
        }
        catch (Exception e)
        {
            toastService.ShowWarning("Could not load modlist image.");
            Logger.LogWarning(e, "Exception loading modlist image for {Name}", ModlistPath);
        }
    }

    private async Task SelectInstallFolder()
    {
        try
        {
            var installPath = await Dialog.ShowDialogNonBlocking(true);
            if (installPath is not null) StateContainer.InstallPath = (AbsolutePath) installPath;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Exception selecting install folder");
        }
    }

    private async Task SelectDownloadFolder()
    {
        try
        {
            var downloadPath = await Dialog.ShowDialogNonBlocking(true);
            if (downloadPath is not null) StateContainer.DownloadPath = (AbsolutePath) downloadPath;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Exception selecting download folder");
        }
    }

    private void Install()
    {
        NavigationManager.NavigateTo(Installing.Route);
    }
}

internal class SavedInstallSettings
{
    public AbsolutePath ModlistLocation { get; set; } = AbsolutePath.Empty;
    public AbsolutePath InstallLocation { get; set; } = AbsolutePath.Empty;
    public AbsolutePath DownloadLocation { get; set; } = AbsolutePath.Empty;
    // public ModlistMetadata Metadata { get; set; }
}
