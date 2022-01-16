using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Shell;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Wabbajack.App.Blazor.Store;
using Wabbajack.App.Blazor.Utility;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;

namespace Wabbajack.App.Blazor.Pages
{
    public partial class Installing
    {
        [Inject] private NavigationManager           NavigationManager      { get; set; }
        [Inject] private IState<InstallState>        _installState          { get; set; }
        [Inject] private DTOSerializer               _dtos                  { get; set; }
        [Inject] private IDispatcher                 _dispatcher            { get; set; }
        [Inject] private IServiceProvider            _serviceProvider       { get; set; }
        [Inject] private SystemParametersConstructor _parametersConstructor { get; set; }
        [Inject] private IGameLocator                _gameLocator           { get; set; }

        private ModList modlist { get; set; }

        private string StatusText { get; set; }

        protected override async Task OnInitializedAsync()
        {
            modlist = _installState.Value.CurrentModList;

            Task.Run(BeginInstall);

            await base.OnInitializedAsync();
        }

        private async Task BeginInstall()
        {
            // var postfix = (await ModListLocation.TargetPath.ToString().Hash()).ToHex();
            // await _settingsManager.Save(InstallSettingsPrefix + postfix, new SavedInstallSettings
            // {
            //     ModListLocation   = ModListLocation.TargetPath,
            //     InstallLocation   = Installer.Location.TargetPath,
            //     DownloadLoadction = Installer.DownloadLocation.TargetPath,
            //     Metadata          = ModlistMetadata
            // });

            try
            {
                var installer = StandardInstaller.Create(_serviceProvider, new InstallerConfiguration
                {
                    Game             = modlist.GameType,
                    Downloads        = (AbsolutePath)_installState.Value.CurrentDownloadPath,
                    Install          = (AbsolutePath)_installState.Value.CurrentInstallPath,
                    ModList          = modlist,
                    ModlistArchive   = (AbsolutePath)_installState.Value.CurrentModListPath,
                    SystemParameters = _parametersConstructor.Create(),
                    GameFolder       = _gameLocator.GameLocation(modlist.GameType)
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
}