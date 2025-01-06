using System;
using System.Linq.Expressions;
using ReactiveUI;

namespace Wabbajack
{
    public static class IViewForExt
    {
        public static IReactiveBinding<TView, TProp> OneWayBindStrict<TViewModel, TView, TProp>(
            this TView view,
            TViewModel viewModel,
            Expression<Func<TViewModel, TProp>> vmProperty,
            Expression<Func<TView, TProp>> viewProperty)
            where TViewModel : class
            where TView : class, IViewFor
        {
            return view.OneWayBind(
                viewModel: viewModel,
                vmProperty: vmProperty,
                viewProperty: viewProperty);
        }

        public static IReactiveBinding<TView, TOut> OneWayBindStrict<TViewModel, TView, TProp, TOut>(
            this TView view,
            TViewModel viewModel,
            Expression<Func<TViewModel, TProp>> vmProperty, 
            Expression<Func<TView, TOut>> viewProperty,
            Func<TProp, TOut> selector)
            where TViewModel : class
            where TView : class, IViewFor
        {
            return view.OneWayBind(
                viewModel: viewModel,
                vmProperty: vmProperty,
                viewProperty: viewProperty,
                selector: selector);
        }

        public static IReactiveBinding<TView, (object view, bool isViewModel)> BindStrict<TViewModel, TView, TProp>(
            this TView view,
            TViewModel viewModel,
            Expression<Func<TViewModel, TProp>> vmProperty,
            Expression<Func<TView, TProp>> viewProperty)
            where TViewModel : class
            where TView : class, IViewFor
        {
            return view.Bind(
                viewModel: viewModel,
                vmProperty: vmProperty,
                viewProperty: viewProperty);
        }

        public static IReactiveBinding<TView, (object view, bool isViewModel)> BindStrict<TViewModel, TView, TVMProp, TVProp, TDontCare>(
            this TView view,
            TViewModel viewModel,
            Expression<Func<TViewModel, TVMProp>> vmProperty,
            Expression<Func<TView, TVProp>> viewProperty,
            IObservable<TDontCare> signalViewUpdate,
            Func<TVMProp, TVProp> vmToViewConverter,
            Func<TVProp, TVMProp> viewToVmConverter)
            where TViewModel : class
            where TView : class, IViewFor
        {
            return view.Bind(
                viewModel: viewModel,
                vmProperty: vmProperty,
                viewProperty: viewProperty,
                signalViewUpdate: signalViewUpdate,
                vmToViewConverter: vmToViewConverter,
                viewToVmConverter: viewToVmConverter);
        }

        public static IReactiveBinding<TView, (object view, bool isViewModel)> BindStrict<TViewModel, TView, TVMProp, TVProp>(
            this TView view,
            TViewModel viewModel,
            Expression<Func<TViewModel, TVMProp>> vmProperty,
            Expression<Func<TView, TVProp>> viewProperty,
            Func<TVMProp, TVProp> vmToViewConverter,
            Func<TVProp, TVMProp> viewToVmConverter)
            where TViewModel : class
            where TView : class, IViewFor
        {
            return view.Bind(
                viewModel: viewModel,
                vmProperty: vmProperty,
                viewProperty: viewProperty,
                vmToViewConverter: vmToViewConverter,
                viewToVmConverter: viewToVmConverter);
        }

        public static IDisposable BindToStrict<TValue, TTarget>(
            this IObservable<TValue> @this,
            TTarget target,
            Expression<Func<TTarget, TValue>> property)
            where TTarget : class
        {
            return @this
                .ObserveOnGuiThread()
                .BindTo<TValue, TTarget, TValue>(target, property);
        }

        /// <summary>
        /// Just a function to signify a field is being used, so it triggers compile errors if it changes
        /// </summary>
        public static void MarkAsNeeded<TView, TViewModel, TVMProp>(
            this TView view,
            TViewModel viewModel,
            Expression<Func<TViewModel, TVMProp>> vmProperty)
            where TViewModel : class
            where TView : class, IViewFor
        {
        }
    }
}
