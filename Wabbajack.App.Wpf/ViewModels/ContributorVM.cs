using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.Logins;
using Wabbajack.LoginManagers;
using Wabbajack.Messages;
using Wabbajack.Networking.GitHub;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using Wabbajack.Util;
using System.Reactive.Linq;

namespace Wabbajack;

public class ContributorVM : ViewModel
{
    private readonly ILogger<ContributorVM> _logger;
    private readonly HttpClient _httpClient;
    private readonly ImageCacheManager _icm;
    private readonly Client _client;

    [Reactive] public Octokit.RepositoryContributor Contributor { get; set; }
    protected ObservableAsPropertyHelper<BitmapImage> _Avatar { get; set; }
    public BitmapImage Avatar => _Avatar.Value;
    [Reactive] public ICommand OpenProfileCommand { get; private set; }

    public ContributorVM(ILogger<ContributorVM> logger, HttpClient httpClient, Octokit.RepositoryContributor contributor, ImageCacheManager icm)
    {
        _logger = logger;
        _httpClient = httpClient;
        _icm = icm;
        Contributor = contributor;

        OpenProfileCommand = ReactiveCommand.Create(OpenProfile);

        var avatarObservable = Observable.Return(Contributor.AvatarUrl)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .DownloadBitmapImage(ex => _logger.LogWarning(ex, "Could not load contributor image for user {Name}", Contributor.Login), LoadingLock, _httpClient, _icm)
            .Replay(1)
            .RefCount(TimeSpan.FromMilliseconds(5000));

        _Avatar = avatarObservable
                    .ToGuiProperty(this, nameof(Avatar))
                    .DisposeWith(CompositeDisposable);

        this.WhenActivated(async disposables =>
        {
            Disposable.Empty.DisposeWith(disposables);
        });
    }

    private void OpenProfile()
    {
        UIUtils.OpenWebsite(Contributor.HtmlUrl);
    }
}
