using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace Wabbajack
{
    public static class IViewForExt
    {
        public static IReactiveBinding<TView, TViewModel, TProp> OneWayBindStrict<TViewModel, TView, TProp>(
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

        public static IReactiveBinding<TView, TViewModel, TOut> OneWayBindStrict<TViewModel, TView, TProp, TOut>(
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

        public static IReactiveBinding<TView, TViewModel, (object view, bool isViewModel)> BindStrict<TViewModel, TView, TProp>(
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

        public static IReactiveBinding<TView, TViewModel, (object view, bool isViewModel)> BindStrict<TViewModel, TView, TVMProp, TVProp>(
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
    }
}
