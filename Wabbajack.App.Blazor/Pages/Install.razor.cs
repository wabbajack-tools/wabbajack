using Microsoft.AspNetCore.Components;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wabbajack.App.Blazor.State;
using Wabbajack.Common;
using Wabbajack.Paths;

namespace Wabbajack.App.Blazor.Pages;

public partial class Install
{
    [Inject] private NavigationManager NavigationManager { get; set; }
    [Inject] private GlobalState       GlobalState       { get; set; }

    private void SelectFile()
    {
        using (var dialog = new CommonOpenFileDialog())
        {
            dialog.Multiselect = false;
            dialog.Filters.Add(new CommonFileDialogFilter("Wabbajack File", "*" + Ext.Wabbajack));
            if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;
            GlobalState.ModListPath = dialog.FileName.ToAbsolutePath();
        }

        NavigationManager.NavigateTo("/Configure");
    }

    private void VerifyFile(AbsolutePath path) { }
}
