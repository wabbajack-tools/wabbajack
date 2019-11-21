namespace Wabbajack.Common.CSP
{
    public class FixedSizeBuffer<T> : IBuffer<T>
    {
        private int _size;
        private RingBuffer<T> _buffer;

        public FixedSizeBuffer(int size)
        {
            _size = size;
            _buffer = new RingBuffer<T>(size);
        }

        public void Dispose()
        {
        }

        public bool IsFull => _buffer.Length >= _size;
        public bool IsEmpty => _buffer.IsEmpty;
        public T Remove()
        {
            return _buffer.Pop();
        }

        public void Add(T itm)
        {
            _buffer.UnboundedUnshift(itm);
        }
    }
}
