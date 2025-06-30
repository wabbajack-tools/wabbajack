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

namespace Wabbajack;

public class AboutVM : ViewModel
{
    private readonly ILogger<AboutVM> _logger;
    private readonly Client _client;
    private readonly IServiceProvider _provider;

    [Reactive] public ObservableCollection<ContributorVM> Contributors { get; private set; }
    public AboutVM(ILogger<AboutVM> logger, Client client, IServiceProvider provider)
    {
        _logger = logger;
        _client = client;
        _provider = provider;

        this.WhenActivated(async disposables =>
        {
            Task.Run(LoadContributors).FireAndForget();

            Disposable.Empty.DisposeWith(disposables);
        });
    }

    private async Task LoadContributors()
    {
        try
        {
            var contributors = await _client.GetWabbajackContributors();
            if (contributors != null)
            {
                var contributorVMs = contributors
                                        .Where(c => !c.Type.Equals("Bot", StringComparison.OrdinalIgnoreCase))
                                        .Select(c =>
                                        {
                                            return new ContributorVM(_provider.GetRequiredService<ILogger<ContributorVM>>(), _provider.GetRequiredService<HttpClient>(), c, _provider.GetRequiredService<ImageCacheManager>());
                                        })
                                        .Take(5); // Sorry! Not enough space for everyone :(
                Contributors = new ObservableCollection<ContributorVM>(contributorVMs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to get Wabbajack GitHub contributors: {ex}", ex.ToString());
        }
    }
}
