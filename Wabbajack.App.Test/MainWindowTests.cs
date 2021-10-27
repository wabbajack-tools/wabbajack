using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Wabbajack.App.Controls;
using Wabbajack.App.Interfaces;
using Wabbajack.App.Screens;
using Wabbajack.App.Views;
using Xunit;

namespace Wabbajack.App.Test;

public class MainWindowTests
{
    private static TimeSpan StandardDelay = new TimeSpan(250);

    [Fact(DisplayName = "Can Open and Close MainWindow")]
    public async Task CanOpenMainApp()
    {
        var app = AvaloniaApp.GetApp();
        var window = AvaloniaApp.GetMainWindow();

        var msv = await GoHome(window);
        msv.BrowseButton.Button.Command.Execute(null);
        msv.BrowseButton.Button.Command.Execute(null);

        var yu = Dispatcher.UIThread;

        await Task.Delay(StandardDelay * 4);

        var gallery = window.GetScreen<BrowseView>();
        gallery.SearchBox.Text = "Halgaris Helper";
        await Task.Delay(StandardDelay);
        var itms = gallery.GalleryList.FindDescendantOfType<BrowseItemView>();




    }

    private async Task<ModeSelectionView> GoHome(MainWindow mainWindow)
    {
        while (mainWindow.BackButton.Command.CanExecute(null))
        {
            mainWindow.BackButton.Command.Execute(null);
            await Task.Delay(StandardDelay);
        }

        if (mainWindow.Contents.Content is ModeSelectionView msv)
            return msv;

        throw new Exception("Top screen is not ModeSelectionView");
    }
}