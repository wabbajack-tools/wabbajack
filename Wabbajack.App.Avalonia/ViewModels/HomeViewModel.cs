using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.Networking.WabbajackClientApi;

namespace Wabbajack.App.Avalonia.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly ILogger<HomeViewModel> _logger;
    private readonly Client _wjClient;

    public HomeViewModel(ILogger<HomeViewModel> logger, Client wjClient, Navigator navigator)
    {
        _logger = logger;
        _wjClient = wjClient;

        BrowseCommand = ReactiveCommand.Create(() => navigator.NavigateTo(ScreenType.ModListGallery));
        VisitModlistWizardCommand = ReactiveCommand.Create(() => Links.Open(Links.ModlistWizard));
        OpenPatreonCommand = ReactiveCommand.Create(() => Links.Open(Links.Patreon));
        OpenGitHubCommand = ReactiveCommand.Create(() => Links.Open(Links.GitHub));
        OpenDiscordCommand = ReactiveCommand.Create(() => Links.Open(Links.Discord));
        OpenWikiCommand = ReactiveCommand.Create(() => Links.Open(Links.Wiki));

        _ = LoadModLists();
    }

    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
    public ReactiveCommand<Unit, Unit> VisitModlistWizardCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenPatreonCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenGitHubCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDiscordCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenWikiCommand { get; }

    // The WPF view left both counts blank until the lists arrived, and blank on a failure; so does this.
    [Reactive] public partial string ModlistCount { get; private set; } = "";
    [Reactive] public partial string GameCount { get; private set; } = "";

    private async Task LoadModLists()
    {
        try
        {
            var lists = await _wjClient.LoadLists();
            ModlistCount = lists.Length.ToString();
            GameCount = lists.GroupBy(l => l.Game).Count().ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While loading lists");
        }
    }
}
