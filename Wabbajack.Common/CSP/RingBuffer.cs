using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace Wabbajack.Common.CSP
{
    public struct RingBuffer<T> : IEnumerable<T>
    {
        private int _size;
        private int _length;
        private int _tail;
        private int _head;
        private T[] _arr;

        public RingBuffer(int size = 8)
        {
            _size = size;
            _arr = new T[size];
            _tail = 0;
            _length = 0;
            _head = 0;
        }

        public T Pop()
        {
            if (_length == 0) return default;
            var val = _arr[_tail];
            _arr[_tail] = default;
            _tail = (_tail + 1) % _size;
            _length -= 1;
            return val;
        }

        public T Peek()
        {
            if (_length == 0) return default;
            return _arr[_tail];
        }

        public void Unshift(T x)
        {
            _arr[_head] = x;
            _head = (_head + 1) % _size;
            _length += 1;
        }

        public void UnboundedUnshift(T x)
        {
            if (_length == _size)
                Resize();
            Unshift(x);
        }

        public bool IsEmpty => _length == 0;
        public int Length => _length;

        private void Resize()
        {
            var new_arr_size = _size * 2;
            var new_arr = new T[new_arr_size];
            if (_tail < _head)
            {
                Array.Copy(_arr, _tail, new_arr, 0, _length);
                _tail = 0;
                _head = _length;
                _arr = new_arr;
                _size = new_arr_size;
            }
            else if (_tail > _head)
            {
                Array.Copy(_arr, _tail, new_arr, 0, _length - _tail);
                Array.Copy(_arr, 0, new_arr, (_length - _tail), _head);
                _tail = 0;
                _head = _length;
                _arr = new_arr;
                _size = new_arr_size;
            }
            else
            {
                _tail = 0;
                _head = 0;
                _arr = new_arr;
                _size = new_arr_size;
            }
        }

        /// <summary>
        /// Filers out all items where should_keep(itm) returns false
        /// </summary>
        /// <param name="should_keep"></param>
        public void Cleanup(Func<T, bool> should_keep)
        {
            for (var idx = 0; idx < _length; idx++)
            {
                var v = Pop();
                if (should_keep(v))
                {
                    Unshift(v);
                }
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            while (!IsEmpty)
                yield return Pop();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
