using System;
using System.Linq;
using System.Reflection;
using Alphaleonis.Win32.Filesystem;
using CommandLine;
using Wabbajack.CLI.Verbs;
using Wabbajack.Common;

namespace Wabbajack.CLI
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class FileAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    internal class DirectoryAttribute : Attribute { }

    internal static class CLIUtils
    {
        internal static bool HasValidArguments(AVerb verb)
        {
            var props = verb.GetType().GetProperties().Where(p =>
            {
                var hasAttr = p.HasAttribute(typeof(OptionAttribute));
                if (!hasAttr)
                    return false;

                if (p.PropertyType != typeof(string))
                    return false;

                var value = p.GetValue(verb);
                if (value == null)
                    return false;

                var stringValue = (string)value;
                return string.IsNullOrWhiteSpace(stringValue);
            });

            var valid = true;

            props.Do(p =>
            {
                if (!valid)
                    return;

                var valueObject = p.GetValue(verb);

                // not really possible since we filtered them out but whatever
                if (valueObject == null)
                    return;

                var value = (string)valueObject;

                if (p.HasAttribute(typeof(FileAttribute)))
                {
                    valid = File.Exists(value);
                }

                if (p.HasAttribute(typeof(DirectoryAttribute)))
                {
                    valid = Directory.Exists(value);
                }
            });

            return valid;
        }

        internal static bool HasAttribute(this MemberInfo member, Type attribute)
        {
            var attributes = member.GetCustomAttributes(attribute);
            return attributes.Count() == 1;
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
