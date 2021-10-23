using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.App.Controls;
using Wabbajack.App.Messages;
using Wabbajack.App.Screens;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.App.Test;

public class GalleryItemTests
{
    private readonly BrowseViewModel _gallery;
    private readonly Configuration _config;

    public GalleryItemTests(BrowseViewModel bvm, Configuration config)
    {
        _config = config;
        _gallery = bvm;
    }
    
    [Fact]
    public async Task CanDownloadGalleryItem()
    {
        foreach (var file in _config.ModListsDownloadLocation.EnumerateFiles().Where(f => f.Extension == Ext.Wabbajack))
        {
            file.Delete();
        }
        
        using var _ = _gallery.Activator.Activate();
        await _gallery.LoadingLock.WaitForLock();
        await _gallery.LoadingLock.WaitForUnlock();
        Assert.True(_gallery.ModLists.Count > 0);

        foreach (var item in _gallery.ModLists)
        {
            Assert.NotEqual(ModListState.Downloading, item.State);
            if (item.State == ModListState.Downloaded)
                Assert.True(item.ModListLocation.FileExists());
            else
                Assert.False(item.ModListLocation.FileExists());
            
            Assert.Equal(Percent.Zero, item.Progress);
        }

        var modList = _gallery.ModLists.First();
        modList.ExecuteCommand.Execute().Subscribe().Dispose();

        var progress = Percent.Zero;
        await modList.WaitUntil(x => x.State == ModListState.Downloading);
        await modList.WaitUntil(x => x.State != ModListState.Downloading, () =>
        {
            Assert.True(modList.Progress >= progress);
            progress = modList.Progress;
        });
        
        Assert.Equal(Percent.Zero, modList.Progress);
        Assert.Equal(ModListState.Downloaded, modList.State); 
        
        
        modList.ExecuteCommand.Execute().Subscribe().Dispose();

        var msgs = ((SimpleMessageBus) MessageBus.Instance).Messages.TakeLast(2).ToArray();

        var configure = msgs.OfType<StartInstallConfiguration>().First();
        Assert.Equal(modList.ModListLocation, configure.ModList);
        
        var navigate = msgs.OfType<NavigateTo>().First();
        Assert.Equal(typeof(InstallConfigurationViewModel), navigate.ViewModel);
    }
}