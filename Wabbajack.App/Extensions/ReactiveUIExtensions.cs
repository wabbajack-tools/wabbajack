using System;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;

namespace Wabbajack.App.Extensions;

public static class ReactiveUIExtensions
{
    public static IObservable<T> OnUIThread<T>(this IObservable<T> src)
    {
        return src.ObserveOn(AvaloniaScheduler.Instance);
    }
}