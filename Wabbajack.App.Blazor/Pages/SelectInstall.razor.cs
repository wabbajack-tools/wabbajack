using System;
using System.Diagnostics;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.Win32;
using Wabbajack.App.Blazor.Store;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;

namespace Wabbajack.App.Blazor.Pages
{
    public partial class SelectInstall
    {
        [Inject] private NavigationManager    NavigationManager { get; set; }
        [Inject] private IState<InstallState> _installState     { get; set; }
        [Inject] private IDispatcher          _dispatcher       { get; set; }

        private void SelectFile()
        {
            var file = new OpenFileDialog
            {
                Filter      = "Wabbajack (*.wabbajack)|*.wabbajack",
                FilterIndex = 1,
                Multiselect = false,
                Title       = "Wabbajack file for install"
            };
            
            try
            {
                if (file.ShowDialog() != true) return;
                var path = file.FileName.ToAbsolutePath();
                VerifyFile(path);
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
            }
        }

        public async void VerifyFile(AbsolutePath path)
        {
            try
            {
                _dispatcher.Dispatch(new UpdateInstallState(InstallState.InstallStateEnum.Configuration, null, path));
                NavigationManager.NavigateTo("/configure");
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
            }
        }
    }
}