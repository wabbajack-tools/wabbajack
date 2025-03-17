using System;

namespace Wabbajack.ViewModels.Controls;

public class RemovableItemViewModel : ViewModel
{
    
    public string Text { get; }

    public Action RemoveFn { get; }

    public RemovableItemViewModel(string text, Action removeFn)
    {
        Text = text;
        RemoveFn = removeFn;

    }
}