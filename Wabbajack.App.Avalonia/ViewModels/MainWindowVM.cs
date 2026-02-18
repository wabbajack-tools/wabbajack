using System;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Avalonia.ViewModels.Gallery;

namespace Wabbajack.App.Avalonia.ViewModels;

public class MainWindowVM : ViewModelBase
{
    [Reactive] public ViewModelBase ActiveContent { get; set; }

    public MainWindowVM(IServiceProvider serviceProvider)
    {
        ActiveContent = serviceProvider.GetRequiredService<ModListGalleryVM>();
    }
}
