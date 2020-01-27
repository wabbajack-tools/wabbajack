using System.Windows;
using MahApps.Metro.Controls;
using ReactiveUI;

namespace Wabbajack.UI
{
    public class ReactiveMetro<TViewModel> : MetroWindow, IViewFor<TViewModel> where TViewModel : class
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                "ViewModel",
                typeof(TViewModel),
                typeof(ReactiveWindow<TViewModel>),
                new PropertyMetadata(null));

        public TViewModel BindingRoot => ViewModel;

        public TViewModel ViewModel
        {
            get => (TViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (TViewModel)value;
        }
    }
}
