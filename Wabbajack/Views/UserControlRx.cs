using ReactiveUI;
using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;

namespace Wabbajack
{
    public class UserControlRx : UserControl, IDisposable, IReactiveObject
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

        private readonly Lazy<CompositeDisposable> _compositeDisposable = new Lazy<CompositeDisposable>();
        public CompositeDisposable CompositeDisposable => _compositeDisposable.Value;

        public virtual void Dispose()
        {
            if (_compositeDisposable.IsValueCreated)
            {
                _compositeDisposable.Value.Dispose();
            }
        }

        protected static void WireNotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is UserControlRx control)) return;
            if (Equals(e.OldValue, e.NewValue)) return;
            control.RaisePropertyChanged(e.Property.Name);
        }
    }

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
            if (!(d is UserControlRx control)) return;
            if (Equals(e.OldValue, e.NewValue)) return;
            control.RaisePropertyChanged(e.Property.Name);
        }
    }
}
