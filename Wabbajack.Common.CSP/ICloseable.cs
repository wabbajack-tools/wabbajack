using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public interface ICloseable
    {
        bool IsClosed { get; }
        void Close();
    }
}
