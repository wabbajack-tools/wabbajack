using ReactiveUI;
using System.ComponentModel;
using System.Windows;

namespace Wabbajack;

public class UserControlRx<TViewModel> : ReactiveUserControl<TViewModel>, IReactiveObject
     where TViewModel : class
{
    public event PropertyChangedEventHandler PropertyChanged;
    public event PropertyChangingEventHandler PropertyChanging;

    public void RaisePropertyChanging(PropertyChangingEventArgs args)
    {
        PropertyChanging?.Invoke(this, args);
    }

    public void RaisePropertyChanged(PropertyChangedEventArgs args)
    {
        PropertyChanged?.Invoke(this, args);
    }

    protected static void WireNotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!(d is UserControlRx<TViewModel> control)) return;
        if (Equals(e.OldValue, e.NewValue)) return;
        control.RaisePropertyChanged(e.Property.Name);
    }
}
