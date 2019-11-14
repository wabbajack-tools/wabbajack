using System;

namespace Wabbajack.Common.CSP
{
    public interface IBuffer<T> : IDisposable
    {
        bool IsFull { get; }
        bool IsEmpty { get; }
        T Remove();
        void Add(T itm);
    }
}
