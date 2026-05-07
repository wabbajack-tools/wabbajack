using System;

namespace Wabbajack.Compiler;

public class CompilerException : Exception
{
    public CompilerException(string msg) : base(msg)
    {
    }

    public CompilerException(string msg, Exception innerException) : base(msg, innerException)
    {
    }
}