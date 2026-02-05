using System;

namespace Wabbajack.Compiler;

public class CompilerException : Exception
{
    public CompilerException(string msg) : base(msg)
    {
    }
}