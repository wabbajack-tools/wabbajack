using System;

namespace Wabbajack.Paths;

public class PathException : Exception
{
    public PathException(string ex) : base(ex)
    {
    }
}