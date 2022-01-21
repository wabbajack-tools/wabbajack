using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Wabbajack.App.Models;

namespace Wabbajack.App.Blazor.Pages;

public partial class Settings
{
    [Inject] private ResourceSettingsManager _resourceSettingsManager { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var resource = await _resourceSettingsManager.GetSettings("Downloads");
            StateHasChanged();
        }
        catch (Exception ex) { }

        await base.OnInitializedAsync();
    }
}
