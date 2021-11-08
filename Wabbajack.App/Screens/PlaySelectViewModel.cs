using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Controls;
using Wabbajack.App.Models;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;

namespace Wabbajack.App.Screens;

public class PlaySelectViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly InstallationStateManager _manager;
    private readonly ImageCache _imageCache;

    public PlaySelectViewModel(InstallationStateManager manager, ImageCache imageCache)
    {
        _imageCache = imageCache;
        _manager = manager;
        Activator = new ViewModelActivator();

        this.WhenActivated(disposables =>
        {
            LoadAndSetItems().FireAndForget();
            Disposable.Empty.DisposeWith(disposables);
        });
    }

    [Reactive] public IEnumerable<InstalledListViewModel> Items { get; set; }

    public async Task LoadAndSetItems()
    {
        var items = await _manager.GetAll();
        Items = items.Settings.Select(a => new InstalledListViewModel(a, _imageCache)).ToArray();
    }
}