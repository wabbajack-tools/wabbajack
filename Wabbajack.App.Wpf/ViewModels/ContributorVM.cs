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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
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

public partial class ContributorVM : ViewModel
{
    private readonly ILogger<ContributorVM> _logger;
    private readonly IImageService _imageService;
    private readonly Client _client;

    [Reactive] public partial Octokit.RepositoryContributor Contributor { get; set; }
    protected ObservableAsPropertyHelper<object> _Avatar { get; set; }
    public object Avatar => _Avatar.Value;
    [Reactive] public partial ICommand OpenProfileCommand { get; private set; }

    public ContributorVM(ILogger<ContributorVM> logger, IImageService imageService, Octokit.RepositoryContributor contributor)
    {
        _logger = logger;
        _imageService = imageService;
        Contributor = contributor;

        OpenProfileCommand = ReactiveCommand.Create(OpenProfile);

        var avatarObservable = _imageService
            .DownloadImage(Observable.Return(Contributor.AvatarUrl), ex => _logger.LogWarning(ex, "Could not load contributor image for user {Name}", Contributor.Login), LoadingLock)
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
