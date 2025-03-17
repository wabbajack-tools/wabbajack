using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive;
using System.Windows.Input;
using Wabbajack.Common;
using Wabbajack.Messages;
using Wabbajack.Networking.WabbajackClientApi;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Wabbajack;

public class HomeVM : ViewModel, ICanGetHelpVM
{
    private readonly ILogger<HomeVM> _logger;
    private readonly Client _wjClient;

    public HomeVM(ILogger<HomeVM> logger, Client wjClient)
    {
        _logger = logger;
        _wjClient = wjClient;
        BrowseCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.ModListGallery));
        GetHelpCommand = ReactiveCommand.Create(() => Process.Start(new ProcessStartInfo("https://wiki.wabbajack.org/") { UseShellExecute = true }));
        VisitModlistWizardCommand = ReactiveCommand.Create(() => Process.Start(new ProcessStartInfo(Consts.WabbajackModlistWizardUri.ToString()) { UseShellExecute = true }));
        LoadModLists().FireAndForget();
    }
    private async Task LoadModLists()
    {
        using var ll = LoadingLock.WithLoading();
        try
        {
            Modlists = await _wjClient.LoadLists();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While loading lists");
            ll.Fail();
        }
        ll.Succeed();
    }

    public ICommand VisitModlistWizardCommand { get; set; }
    public ICommand BrowseCommand { get; set; }
    public ReactiveCommand<Unit, Unit> UpdateCommand { get; set; }

    [Reactive]
    public ModlistMetadata[] Modlists { get; private set; }

    public ICommand GetHelpCommand { get; set; }
}
