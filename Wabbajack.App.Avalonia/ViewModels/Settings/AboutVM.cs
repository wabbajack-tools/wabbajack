using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Common;
using Wabbajack.Networking.GitHub;

namespace Wabbajack.App.Avalonia.ViewModels.Settings;

/// <summary>The about card: the logo, and the first five human contributors from GitHub.</summary>
public partial class AboutVM : ViewModel
{
    private readonly ILogger<AboutVM> _logger;
    private readonly Client _client;
    private readonly IServiceProvider _provider;

    public AboutVM(ILogger<AboutVM> logger, Client client, IServiceProvider provider)
    {
        _logger = logger;
        _client = client;
        _provider = provider;

        this.WhenActivated((CompositeDisposable _) => Task.Run(LoadContributors).FireAndForget());
    }

    [Reactive] public partial ObservableCollection<ContributorVM>? Contributors { get; private set; }

    private async Task LoadContributors()
    {
        try
        {
            var contributors = await _client.GetWabbajackContributors();
            if (contributors == null) return;

            var vms = contributors
                .Where(c => !c.Type.Equals("Bot", StringComparison.OrdinalIgnoreCase))
                .Take(5) // Not enough space for everyone.
                .ToList();

            RxApp.MainThreadScheduler.Schedule(() =>
                Contributors = new ObservableCollection<ContributorVM>(vms.Select(c => new ContributorVM(
                    _provider.GetRequiredService<ILogger<ContributorVM>>(),
                    _provider.GetRequiredService<HttpClient>(), c,
                    _provider.GetRequiredService<ImageCacheManager>()))));
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to get Wabbajack GitHub contributors: {ex}", ex.ToString());
        }
    }
}

/// <summary>One contributor's avatar, which opens their GitHub profile.</summary>
public partial class ContributorVM : ViewModel
{
    private readonly ObservableAsPropertyHelper<Bitmap?> _avatar;

    public ContributorVM(ILogger<ContributorVM> logger, HttpClient httpClient,
        Octokit.RepositoryContributor contributor, ImageCacheManager icm)
    {
        Contributor = contributor;
        OpenProfileCommand = ReactiveCommand.Create(() => UIUtils.OpenWebsite(Contributor.HtmlUrl));

        _avatar = Observable.Return(Contributor.AvatarUrl)
            .DownloadBitmap(ex => logger.LogWarning(ex, "Could not load contributor image for user {Name}", Contributor.Login),
                LoadingLock, httpClient, icm)
            .ToProperty(this, nameof(Avatar));
    }

    public Octokit.RepositoryContributor Contributor { get; }
    public Bitmap? Avatar => _avatar.Value;
    public ICommand OpenProfileCommand { get; }
}
