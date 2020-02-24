using System;

namespace Wabbajack.CLI
{
    internal static class CLIUtils
    {
        internal static void Log(string msg, bool newLine = true)
        {
            //TODO: maybe also write to a log file?
            if(newLine)
                Console.WriteLine(msg);
            else
                Console.Write(msg);
        }

        internal static void LogException(Exception e, string msg)
        {
            Console.WriteLine($"{msg}\n{e}");
        }
    }
}
