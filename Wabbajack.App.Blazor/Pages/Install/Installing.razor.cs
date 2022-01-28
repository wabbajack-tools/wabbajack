using System;
using System.Collections.Generic;
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
using Wabbajack.App.Blazor.State;

namespace Wabbajack.App.Blazor.Pages;

public partial class Installing
{
    [Inject] private ILogger<Configure> Logger { get; set; } = default!;
    [Inject] private IStateContainer StateContainer { get; set; } = default!;
    [Inject] private DTOSerializer DTOs { get; set; } = default!;
    [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;
    [Inject] private SystemParametersConstructor ParametersConstructor { get; set; } = default!;
    [Inject] private IGameLocator GameLocator { get; set; } = default!;
    [Inject] private SettingsManager SettingsManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private ModList? Modlist => StateContainer.Modlist;
    private string ModlistImage => StateContainer.ModlistImage;
    private AbsolutePath ModlistPath => StateContainer.ModlistPath;
    private AbsolutePath InstallPath => StateContainer.InstallPath;
    private AbsolutePath DownloadPath => StateContainer.DownloadPath;

    public string StatusCategory { get; set; }

    private string LastStatus { get; set; }

    public List<string> StatusStep { get; set; } = new();

    private InstallState InstallState => StateContainer.InstallState;

    private const string InstallSettingsPrefix = "install-settings-";

    private bool _shouldRender;
    protected override bool ShouldRender() => _shouldRender;

    protected override void OnInitialized()
    {
        Install();
        _shouldRender = true;
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
                if (LastStatus == update.StatusText) return;
                StatusStep.Insert(0, update.StatusText);
                StatusCategory = update.StatusCategory;
                LastStatus = update.StatusText;
                InvokeAsync(StateHasChanged);
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
