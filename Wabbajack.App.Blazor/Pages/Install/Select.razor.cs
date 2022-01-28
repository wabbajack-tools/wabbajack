using Microsoft.AspNetCore.Components;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wabbajack.App.Blazor.State;
using Wabbajack.Common;
using Wabbajack.Paths;

namespace Wabbajack.App.Blazor.Pages;

public partial class Select
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IStateContainer StateContainer { get; set; } = default!;

    private void SelectFile()
    {
        using (var dialog = new CommonOpenFileDialog())
        {
            dialog.Multiselect = false;
            dialog.Filters.Add(new CommonFileDialogFilter("Wabbajack File", "*" + Ext.Wabbajack));
            if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;
            StateContainer.ModlistPath = dialog.FileName.ToAbsolutePath();
        }

        NavigationManager.NavigateTo(Configure.Route);
    }
}
