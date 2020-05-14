using System.Collections.Generic;

namespace Wabbajack.Common
{
    public class ConcurrentHashSet<T> where T : notnull
    {
        private Dictionary<T, bool> _inner;

        public ConcurrentHashSet()
        {
            _inner = new Dictionary<T, bool>();
        }
        public ConcurrentHashSet(IEnumerable<T> input)
        {
            _inner = new Dictionary<T, bool>();
            foreach (var itm in input)
                Add(itm);
        }

        public bool Contains(T key)
        {
            return _inner.ContainsKey(key);
        }

        public void Add(T key)
        {
            _inner[key] = true;
        }
    }
}
