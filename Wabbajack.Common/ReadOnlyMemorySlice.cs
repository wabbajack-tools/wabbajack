using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Wabbajack.Common;

public struct ReadOnlyMemorySlice<T> : IEnumerable<T>
{
    private T[] _arr;
    public int Length { get; private set; }

    public int StartPosition { get; private set; }

    [DebuggerStepThrough]
    public ReadOnlyMemorySlice(T[] arr)
    {
        _arr = arr;
        StartPosition = 0;
        Length = arr.Length;
    }

    [DebuggerStepThrough]
    public ReadOnlyMemorySlice(T[] arr, int startPos, int length)
    {
        _arr = arr;
        StartPosition = startPos;
        Length = length;
    }

    public ReadOnlySpan<T> Span => _arr.AsSpan(StartPosition, Length);

    public T this[int index] => _arr[index + StartPosition];

    [DebuggerStepThrough]
    public ReadOnlyMemorySlice<T> Slice(int start)
    {
        var startPos = StartPosition + start;
        if (startPos < 0) throw new ArgumentOutOfRangeException();
        return new ReadOnlyMemorySlice<T>
        {
            _arr = _arr,
            StartPosition = StartPosition + start,
            Length = Length - start
        };
    }

    [DebuggerStepThrough]
    public ReadOnlyMemorySlice<T> Slice(int start, int length)
    {
        var startPos = StartPosition + start;
        if (startPos < 0) throw new ArgumentOutOfRangeException();
        if (startPos + length > _arr.Length) throw new ArgumentOutOfRangeException();
        return new ReadOnlyMemorySlice<T>
        {
            _arr = _arr,
            StartPosition = StartPosition + start,
            Length = length
        };
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Length; i++) yield return _arr[i + StartPosition];
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static implicit operator ReadOnlySpan<T>(ReadOnlyMemorySlice<T> mem)
    {
        return mem.Span;
    }

    public static implicit operator ReadOnlyMemorySlice<T>?(T[]? mem)
    {
        if (mem == null) return null;
        return new ReadOnlyMemorySlice<T>(mem);
    }

    public static implicit operator ReadOnlyMemorySlice<T>(T[] mem)
    {
        return new ReadOnlyMemorySlice<T>(mem);
    }
}