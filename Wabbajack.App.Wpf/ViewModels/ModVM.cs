using ReactiveUI;
using System;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack;

public class ModVM : ViewModel
{
    private readonly ILogger<ModVM> _logger;
    private readonly IImageService _imageService;
    public IMetaState State { get; }

    // Image isn't exposed as a direct property, but as an observable.
    // This acts as a caching mechanism, as interested parties will trigger it to be created,
    // and the cached image will automatically be released when the last interested party is gone.
    public IObservable<object> ImageObservable { get; }

    public ModVM(ILogger<ModVM> logger, IMetaState state, IImageService imageService)
    {
        _logger = logger;
        _imageService = imageService;
        State = state;

        ImageObservable = _imageService
            .DownloadImage(Observable.Return(State.ImageURL?.ToString()), ex => _logger.LogWarning(ex, "Skipping slide for mod {Name}", State.Name), LoadingLock)
            .Replay(1)
            .RefCount(TimeSpan.FromMilliseconds(5000));
    }
}
