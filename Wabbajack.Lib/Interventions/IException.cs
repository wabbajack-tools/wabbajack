using System;

namespace Wabbajack.Lib.Interventions
{
    public interface IException : IError
    {
        Exception Exception { get; }
    }
}
