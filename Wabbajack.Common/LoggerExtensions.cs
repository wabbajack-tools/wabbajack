using System;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Common;

public static class LoggerExtensions
{
    public static void CatchAndLog(this ILogger logger, Action fn)
    {
        try
        {
            fn();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "In Catch and log");
        }
    }
    
}