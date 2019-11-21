using System.Collections.Generic;
using System.IO;

namespace Wabbajack.Common.CSP
{
    class EnumeratorBuffer<T> : IBuffer<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private bool _empty;

        public EnumeratorBuffer(IEnumerator<T> enumerator)
        {
            _enumerator = enumerator;
            _empty = !_enumerator.MoveNext();
        }

        public void Dispose()
        {
        }

        public bool IsFull => true;
        public bool IsEmpty => _empty;
        public T Remove()
        {
            var val = _enumerator.Current;
            _empty = !_enumerator.MoveNext();
            return val;
        }

        public void Add(T itm)
        {
            throw new InvalidDataException();
        }
    }
}
