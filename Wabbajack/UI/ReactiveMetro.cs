using System.Windows;
using MahApps.Metro.Controls;
using ReactiveUI;

namespace Wabbajack.UI
{
    /// <summary>
    /// A <see cref="MetroWindow"/> that is reactive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is a <see cref="MetroWindow"/> that is also reactive. That is, it implements <see cref="IViewFor{TViewModel}"/>.
    /// You can extend this class to get an implementation of <see cref="IViewFor{TViewModel}"/> rather than writing one yourself.
    /// </para>
    /// </remarks>
    /// <typeparam name="TViewModel">
    /// The type of the view model backing the view.
    /// </typeparam>
    public class ReactiveMetro<TViewModel> : MetroWindow, IViewFor<TViewModel> where TViewModel : class
    {
        /// <summary>
        /// The view model dependency property.
        /// </summary>
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                "ViewModel",
                typeof(TViewModel),
                typeof(ReactiveWindow<TViewModel>),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets the binding root view model.
        /// </summary>
        public TViewModel BindingRoot => ViewModel;

        /// <inheritdoc/>
        public TViewModel ViewModel
        {
            get => (TViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        /// <inheritdoc/>
        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (TViewModel)value;
        }
    }
}
