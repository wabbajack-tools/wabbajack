using System;

namespace Wabbajack.App.Messages;

public record Error(string Prefix, Exception Exception)
{
    
}