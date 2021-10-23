using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Controls;

public class TagViewModel : ViewModelBase, IActivatableViewModel
{
    public TagViewModel(string name, string tag)
    {
        Activator = new ViewModelActivator();
        Name = name;
        Tag = tag;
    }

    [Reactive] public string Name { get; set; }

    [Reactive] public string Tag { get; set; }
}