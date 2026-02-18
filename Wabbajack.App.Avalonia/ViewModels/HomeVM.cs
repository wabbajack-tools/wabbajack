using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Wabbajack.Common;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;

namespace Wabbajack.App.Avalonia.ViewModels;

public class HomeVM : ViewModelBase
{
    private readonly ILogger<HomeVM> _logger;
    private readonly Client _wjClient;

    [Reactive] public ModlistMetadata[]? Modlists { get; private set; }

    public ICommand BrowseCommand { get; }
    public ICommand VisitModlistWizardCommand { get; }

    public HomeVM(ILogger<HomeVM> logger, Client wjClient)
    {
        _logger = logger;
        _wjClient = wjClient;

        BrowseCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.ModListGallery));
        VisitModlistWizardCommand = ReactiveCommand.Create(() =>
            Process.Start(new ProcessStartInfo("https://wizard.wabbajack.org") { UseShellExecute = true }));

        LoadModListsAsync().FireAndForget();
    }

    private async Task LoadModListsAsync()
    {
        using var ll = LoadingLock.WithLoading();
        try
        {
            Modlists = await _wjClient.LoadLists();
            ll.Succeed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While loading modlists");
            ll.Fail();
        }
    }
}
