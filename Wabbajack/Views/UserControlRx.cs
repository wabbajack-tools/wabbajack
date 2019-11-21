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

        private readonly Lazy<CompositeDisposable> _CompositeDisposable = new Lazy<CompositeDisposable>();
        public CompositeDisposable CompositeDisposable => _CompositeDisposable.Value;

        public virtual void Dispose()
        {
            if (_CompositeDisposable.IsValueCreated)
            {
                _CompositeDisposable.Value.Dispose();
            }
        }

        protected static void WireNotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is UserControlRx control)) return;
            if (Equals(e.OldValue, e.NewValue)) return;
            control.RaisePropertyChanged(e.Property.Name);
        }
    }
}
