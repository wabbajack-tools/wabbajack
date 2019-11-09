using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;

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
