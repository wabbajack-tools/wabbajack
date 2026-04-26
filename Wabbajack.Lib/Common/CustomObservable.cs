using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace Wabbajack.Common;

public class CustomObservable<T> : IObservable<T>
{
    private readonly Subject<T> _subject = new();
    private readonly IEqualityComparer<T> _equalityComparer;

    private T _value;
    public T Value
    {
        get => _value;
        set
        {
            if (_equalityComparer.Equals(value, _value)) return;
            _value = value;
            _subject.OnNext(value);
        } 
    }

    public CustomObservable(T value, IEqualityComparer<T>? equalityComparer = null)
    {
        _value = value;
        _equalityComparer = equalityComparer ?? EqualityComparer<T>.Default;
    }

    public IDisposable Subscribe(IObserver<T> observer) => _subject.Subscribe(observer);
}