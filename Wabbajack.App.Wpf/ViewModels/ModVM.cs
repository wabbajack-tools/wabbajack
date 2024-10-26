using ReactiveUI;
using System;
using System.Net.Http;
using System.Reactive.Linq;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack;

public class ModVM : ViewModel
{
    private readonly ILogger<ModVM> _logger;
    private readonly IServiceProvider _serviceProvider;
    private HttpClient _httpClient;
    public IMetaState State { get; }

    // Image isn't exposed as a direct property, but as an observable.
    // This acts as a caching mechanism, as interested parties will trigger it to be created,
    // and the cached image will automatically be released when the last interested party is gone.
    public IObservable<BitmapImage> ImageObservable { get; }

    public ModVM(ILogger<ModVM> logger, IServiceProvider serviceProvider, IMetaState state)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _httpClient = _serviceProvider.GetService<HttpClient>();
        State = state;

        ImageObservable = Observable.Return(State.ImageURL?.ToString())
            .ObserveOn(RxApp.TaskpoolScheduler)
            .DownloadBitmapImage(ex => _logger.LogWarning(ex, "Skipping slide for mod {Name}", State.Name), LoadingLock, _httpClient)
            .Replay(1)
            .RefCount(TimeSpan.FromMilliseconds(5000));
    }
}
