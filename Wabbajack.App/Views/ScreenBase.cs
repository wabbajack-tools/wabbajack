using System;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Interfaces;
using Wabbajack.App.ViewModels;

namespace Wabbajack.App.Views;

public abstract class ScreenBase<T> : ViewBase<T>, IScreenView
    where T : ViewModelBase
{
    protected ScreenBase(string humanName, bool createViewModel = true) : base(createViewModel)
    {
        HumanName = humanName;
    }

    public Type ViewModelType => typeof(T);
    
    [Reactive]
    public string HumanName { get; set; }
}