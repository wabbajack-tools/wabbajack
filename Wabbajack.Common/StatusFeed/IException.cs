using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.StatusFeed
{
    public interface IException : IError
    {
        Exception Exception { get; }
    }
}
