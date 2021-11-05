using System;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ReactiveUI;

namespace Wabbajack.App.Extensions;

public static class IObservableExtensions
{
    public static IDisposable SimpleOneWayBind<TView, TViewModel, TProp, TOut>(
        this TView view,
        TViewModel? viewModel,
        Expression<Func<TViewModel, TProp?>> vmProperty,
        Expression<Func<TView, TOut?>> viewProperty)
        where TView : class
    {
        var d = viewModel.WhenAny(vmProperty, change => change.Value)
            .ObserveOn(RxApp.MainThreadScheduler)
            .BindTo(view, viewProperty);

        return Disposable.Create(() => d.Dispose());
    }

    public static IDisposable SimpleOneWayBind<TView, TViewModel, TProp, TOut>(
        this TView view,
        TViewModel? viewModel,
        Expression<Func<TViewModel, TProp?>> vmProperty,
        Expression<Func<TView, TOut?>> viewProperty,
        Func<TProp?, TOut> selector)
        where TView : class
    {
        var d = viewModel.WhenAnyValue(vmProperty)
            .Select(change => selector(change))
            .ObserveOn(RxApp.MainThreadScheduler)
            .BindTo(view, viewProperty);

        return Disposable.Create(() => d.Dispose());
    }
}