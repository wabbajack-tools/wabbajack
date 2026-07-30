using System;

namespace Wabbajack.Interventions;

public interface IException : IError
{
    Exception Exception { get; }
}
