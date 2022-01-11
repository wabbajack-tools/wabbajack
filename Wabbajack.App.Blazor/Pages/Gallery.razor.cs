using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.DTOs;

namespace Wabbajack.App.Blazor.Pages;

public partial class Gallery
{
    List<ModlistMetadata> _listItems = new();

    protected override async Task<Task> OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return base.OnAfterRenderAsync(firstRender);

        try
        {
            ModlistMetadata[] modLists = await _client.LoadLists();
            _listItems = modLists.ToList();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        return base.OnAfterRenderAsync(firstRender);
    }
}