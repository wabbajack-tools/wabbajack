using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wabbajack.App.Blazor.Store;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Ookii.Dialogs.Wpf;
using Wabbajack.App.Blazor.Utility;

namespace Wabbajack.App.Blazor.Pages
{
    public partial class Configure
    {
        [Inject] private NavigationManager    NavigationManager { get; set; }
        [Inject] private IState<InstallState> _installState     { get; set; }
        [Inject] private DTOSerializer        _dtos             { get; set; }
        [Inject] private IDispatcher          _dispatcher       { get; set; }

        private string       Name         { get; set; } = "";
        private string       Author       { get; set; } = "";
        private string       Description  { get; set; } = "";
        private Version      Version      { get; set; } = Version.Parse("0.0.0");
        private string       Image        { get; set; } = "";
        private ModList      ModList      { get; set; }
        private AbsolutePath ModListPath  { get; set; }
        private AbsolutePath InstallPath  { get; set; }
        private AbsolutePath DownloadPath { get; set; }

        protected override async Task OnInitializedAsync()
        {
            // var Location = KnownFolders.EntryPoint.Combine("downloaded_mod_lists", machineURL).WithExtension(Ext.Wabbajack);

            await CheckValidInstallPath();

            await base.OnInitializedAsync();
        }

        private async Task CheckValidInstallPath()
        {
            if (_installState.Value.CurrentModListPath == null) return;

            ModListPath = (AbsolutePath)_installState.Value.CurrentModListPath;
            ModList     = await StandardInstaller.LoadFromFile(_dtos, ModListPath);
            _dispatcher.Dispatch(new UpdateInstallState(InstallState.InstallStateEnum.Configuration, ModList, ModListPath, null, null));
            
            Name        = _installState.Value.CurrentModList.Name;
            Author      = _installState.Value.CurrentModList.Author;
            Description = _installState.Value.CurrentModList.Description;
            Version     = _installState.Value.CurrentModList.Version;
            ModListPath = (AbsolutePath)_installState.Value.CurrentModListPath;

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

        private void NavigateInstall()
        {
            _dispatcher.Dispatch(new UpdateInstallState(InstallState.InstallStateEnum.Configuration, ModList, ModListPath, InstallPath, DownloadPath));
            NavigationManager.NavigateTo("installing");
        }
    }
}