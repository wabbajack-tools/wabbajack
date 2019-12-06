using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.StatusFeed
{
    public interface IStatusMessage
    {
        DateTime Timestamp { get; }
        string ShortDescription { get; }
        string ExtendedDescription { get; }
    }
}
