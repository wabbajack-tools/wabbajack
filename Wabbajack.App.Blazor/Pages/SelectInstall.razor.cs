using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DynamicData;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wabbajack.App.Blazor.Store;
using Wabbajack.Common;
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

        private AbsolutePath _modListPath { get; set; }

        private void SelectFile()
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Multiselect = false;
                dialog.Filters.Add(new CommonFileDialogFilter("Wabbajack File", "*" + Ext.Wabbajack));
                if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;
                _modListPath = dialog.FileName.ToAbsolutePath();
            }
            VerifyFile(_modListPath);
        }

        // private void SelectFile()
        // {
        //     var file = new OpenFileDialog
        //     {
        //         Filter      = "Wabbajack (*.wabbajack)|*.wabbajack",
        //         FilterIndex = 1,
        //         Multiselect = false,
        //         Title       = "Wabbajack file for install"
        //     };
        //     
        //     try
        //     {
        //         if (file.ShowDialog() != true) return;
        //         var path = file.FileName.ToAbsolutePath();
        //         VerifyFile(path);
        //     }
        //     catch (Exception ex)
        //     {
        //         Debug.Print(ex.Message);
        //     }
        // }

        private void VerifyFile(AbsolutePath path)
        {
            try
            {
                _dispatcher.Dispatch(new UpdateInstallState(InstallState.InstallStateEnum.Configuration, null, path, null, null));
                NavigationManager.NavigateTo("/configure");
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
            }
        }
    }
}