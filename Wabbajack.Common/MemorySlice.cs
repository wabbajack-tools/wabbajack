using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Wabbajack.Common
{
    public struct MemorySlice<T> : IEnumerable<T>
    {
        private T[] _arr;
        private int _startPos;
        private int _length;
        public int Length => _length;
        public int StartPosition => _startPos;

        [DebuggerStepThrough]
        public MemorySlice(T[] arr)
        {
            this._arr = arr;
            this._startPos = 0;
            this._length = arr.Length;
        }

        [DebuggerStepThrough]
        public MemorySlice(T[] arr, int startPos, int length)
        {
            this._arr = arr;
            this._startPos = startPos;
            this._length = length;
        }

        public Span<T> Span => _arr.AsSpan(start: _startPos, length: _length);

        public T this[int index]
        {
            get => _arr[index + _startPos];
            set => _arr[index + _startPos] = value;
        }

        [DebuggerStepThrough]
        public MemorySlice<T> Slice(int start)
        {
            var startPos = _startPos + start;
            if (startPos < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            return new MemorySlice<T>()
            {
                _arr = _arr,
                _startPos = startPos,
                _length = _length - start
            };
        }

        [DebuggerStepThrough]
        public MemorySlice<T> Slice(int start, int length)
        {
            var startPos = _startPos + start;
            if (startPos < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            if (startPos + length > _arr.Length)
            {
                throw new ArgumentOutOfRangeException();
            }
            return new MemorySlice<T>()
            {
                _arr = _arr,
                _startPos = startPos,
                _length = length
            };
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _length; i++)
            {
                yield return this._arr[i + _startPos];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public static implicit operator ReadOnlyMemorySlice<T>(MemorySlice<T> mem)
        {
            return new ReadOnlyMemorySlice<T>(
                mem._arr,
                mem._startPos,
                mem._length);
        }

        public static implicit operator ReadOnlySpan<T>(MemorySlice<T> mem)
        {
            return mem.Span;
        }

        public static implicit operator Span<T>(MemorySlice<T> mem)
        {
            return mem.Span;
        }

        public static implicit operator MemorySlice<T>(T[] mem)
        {
            return new MemorySlice<T>(mem);
        }

        public static implicit operator MemorySlice<T>?(T[]? mem)
        {
            if (mem == null) return null;
            return new MemorySlice<T>(mem);
        }
    }

    public struct ReadOnlyMemorySlice<T> : IEnumerable<T>
    {
        private T[] _arr;
        private int _startPos;
        private int _length;
        public int Length => _length;
        public int StartPosition => _startPos;

        [DebuggerStepThrough]
        public ReadOnlyMemorySlice(T[] arr)
        {
            this._arr = arr;
            this._startPos = 0;
            this._length = arr.Length;
        }

        [DebuggerStepThrough]
        public ReadOnlyMemorySlice(T[] arr, int startPos, int length)
        {
            this._arr = arr;
            this._startPos = startPos;
            this._length = length;
        }

        public ReadOnlySpan<T> Span => _arr.AsSpan(start: _startPos, length: _length);

        public T this[int index] => _arr[index + _startPos];

        [DebuggerStepThrough]
        public ReadOnlyMemorySlice<T> Slice(int start)
        {
            var startPos = _startPos + start;
            if (startPos < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            return new ReadOnlyMemorySlice<T>()
            {
                _arr = _arr,
                _startPos = _startPos + start,
                _length = _length - start
            };
        }

        [DebuggerStepThrough]
        public ReadOnlyMemorySlice<T> Slice(int start, int length)
        {
            var startPos = _startPos + start;
            if (startPos < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            if (startPos + length > _arr.Length)
            {
                throw new ArgumentOutOfRangeException();
            }
            return new ReadOnlyMemorySlice<T>()
            {
                _arr = _arr,
                _startPos = _startPos + start,
                _length = length
            };
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _length; i++)
            {
                yield return this._arr[i + _startPos];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

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

    public static class MemorySliceExt
    {
        public static bool Equal<T>(ReadOnlyMemorySlice<T>? lhs, ReadOnlyMemorySlice<T>? rhs)
            where T : IEquatable<T>
        {
            if (lhs == null && rhs == null) return true;
            if (lhs == null || rhs == null) return false;
            return MemoryExtensions.SequenceEqual(lhs.Value.Span, rhs.Value.Span);
        }
    }
}
