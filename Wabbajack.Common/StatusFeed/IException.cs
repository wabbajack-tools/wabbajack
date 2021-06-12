using System;

namespace Wabbajack.Common.StatusFeed
{
    public interface IException : IError
    {
        Exception Exception { get; }
    }
}
