using System;
using System.Collections.Generic;
using System.Reactive;

namespace Wabbajack.Common;

public class CustomObservable<T> : ObservableBase<T>
{
    private readonly List<IObserver<T>> _observers = new();

    private T _value;
    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(value, _value)) return;
            _value = value;

            foreach (var observer in _observers)
            {
                observer.OnNext(value);
            }
        } 
    }

    public CustomObservable(T value)
    {
        _value = value;
    }

    protected override IDisposable SubscribeCore(IObserver<T> observer)
    {
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
            observer.OnNext(Value);
        }

        return new Unsubscriber<T>(_observers, observer);
    }
}

internal sealed class Unsubscriber<T> : IDisposable
{
    private readonly List<IObserver<T>> _observers;
    private readonly IObserver<T> _observer;

    public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer)
    {
        _observers = observers;
        _observer = observer;
    }

    public void Dispose()
    {
        if (_observers.Contains(_observer)) _observers.Remove(_observer);
    }
}