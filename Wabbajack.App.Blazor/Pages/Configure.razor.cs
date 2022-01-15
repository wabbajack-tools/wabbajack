using System;
using System.IO;
using System.Threading.Tasks;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Wabbajack.App.Blazor.Store;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;

namespace Wabbajack.App.Blazor.Pages
{
    public partial class Configure
    {
        [Inject] private NavigationManager    NavigationManager { get; set; }
        [Inject] private IState<InstallState> _installState     { get; set; }
        [Inject] private DTOSerializer        _dtos             { get; set; }
        [Inject] private IDispatcher          _dispatcher       { get; set; }

        private string  Name        { get; set; }
        private string  Author      { get; set; }
        private string  Description { get; set; }
        private Version Version     { get; set; }
        private string  Image       { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await CheckValidInstallPath();

            await base.OnInitializedAsync();
        }

        private async Task CheckValidInstallPath()
        {
            if (_installState.Value.CurrentPath == null) return;

            var listPath = (AbsolutePath)_installState.Value.CurrentPath;
            ModList modList = await StandardInstaller.LoadFromFile(_dtos, listPath);
            _dispatcher.Dispatch(new UpdateInstallState(InstallState.InstallStateEnum.Configuration, modList, listPath));


            Name        = _installState.Value.CurrentModlist.Name;
            Author      = _installState.Value.CurrentModlist.Author;
            Description = _installState.Value.CurrentModlist.Description;
            Version     = _installState.Value.CurrentModlist.Version;

            var imagepath = (AbsolutePath)_installState.Value.CurrentPath;
            Stream image = await StandardInstaller.ModListImageStream(imagepath);
            await using var reader = new MemoryStream();
            await image.CopyToAsync(reader);
            Image = $"data:image/png;base64,{Convert.ToBase64String(reader.ToArray())}";
        }
    }
}