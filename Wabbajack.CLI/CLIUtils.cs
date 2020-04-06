using System;
using Wabbajack.CLI.Verbs;

namespace Wabbajack.CLI
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class FileAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    internal class DirectoryAttribute : Attribute { }

    internal static class CLIUtils
    {
        internal static bool VerifyArguments(AVerb verb)
        {
            return true;
        }

        internal static void Log(string msg, bool newLine = true)
        {
            //TODO: maybe also write to a log file?
            if(newLine)
                Console.WriteLine(msg);
            else
                Console.Write(msg);
        }

        internal static int Exit(string msg, int code)
        {
            Log(msg);
            return code;
        }

        internal static void LogException(Exception e, string msg)
        {
            Console.WriteLine($"{msg}\n{e}");
        }
    }
}
