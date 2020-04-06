using System;
using System.Linq;
using System.Reflection;
using Alphaleonis.Win32.Filesystem;
using CommandLine;
using Wabbajack.CLI.Verbs;
using Wabbajack.Common;

namespace Wabbajack.CLI
{
    internal enum ExitCode
    {
        BadArguments = -1,
        Ok = 0,
        Error = 1
    }

    /// <summary>
    /// Abstract class to mark attributes which need validating
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    internal abstract class AValidateAttribute : Attribute
    {
        /// <summary>
        /// Custom message if validation failed. Use placeholder %1 to insert the value
        /// </summary>
        public string? CustomMessage { get; set; }
    }

    /// <summary>
    /// Validating if the file exists
    /// </summary>
    internal class IsFileAttribute : AValidateAttribute { }

    /// <summary>
    /// Validating if the directory exists
    /// </summary>
    internal class IsDirectoryAttribute : AValidateAttribute
    {
        /// <summary>
        /// Create the directory if it does not exists
        /// </summary>
        public bool Create { get; set; }
    }

    internal static class CLIUtils
    {
        /// <summary>
        /// Validates all Attributes of type <see cref="AValidateAttribute"/>
        /// </summary>
        /// <param name="verb">The verb to validate</param>
        /// <returns></returns>
        internal static bool HasValidArguments(AVerb verb)
        {
            var props = verb.GetType().GetProperties().Where(p =>
            {
                var hasAttr = p.HasAttribute(typeof(OptionAttribute)) 
                              && p.HasAttribute(typeof(AValidateAttribute));
                if (!hasAttr)
                    return false;

                if (p.PropertyType != typeof(string))
                    return false;

                var value = p.GetValue(verb);
                if (value == null)
                    return false;

                var stringValue = (string)value;
                return !string.IsNullOrWhiteSpace(stringValue);
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
                var attribute = (AValidateAttribute)p.GetAttribute(typeof(AValidateAttribute));
                var isFile = false;

                if (p.HasAttribute(typeof(IsFileAttribute)))
                {
                    isFile = true;
                    valid = File.Exists(value);
                }

                if (p.HasAttribute(typeof(IsDirectoryAttribute)))
                {
                    var dirAttribute = (IsDirectoryAttribute)attribute;
                    var exists = Directory.Exists(value);

                    if (!exists)
                    {
                        if (dirAttribute.Create)
                        {
                            Log($"Directory {value} does not exist and will be created");
                            Directory.CreateDirectory(value);
                        }
                        else
                            valid = false;
                    }
                }

                if (valid)
                    return;

                var message = string.IsNullOrWhiteSpace(attribute.CustomMessage) 
                    ? isFile 
                        ? $"The file {value} does not exist!"
                        : $"The folder {value} does not exist!"
                    : attribute.CustomMessage.Replace("%1", value);

                var optionAttribute = (OptionAttribute)p.GetAttribute(typeof(OptionAttribute));

                if (optionAttribute.Required)
                    Exit(message, ExitCode.BadArguments);
                else
                    Log(message);
            });

            return valid;
        }

        /// <summary>
        /// Gets an attribute of a specific type
        /// </summary>
        /// <param name="member"></param>
        /// <param name="attribute"></param>
        /// <returns></returns>
        internal static Attribute GetAttribute(this MemberInfo member, Type attribute)
        {
            var attributes = member.GetCustomAttributes(attribute);
            return attributes.ElementAt(0);
        }

        /// <summary>
        /// Checks if a <see cref="MemberInfo"/> has a custom attribute
        /// </summary>
        /// <param name="member"></param>
        /// <param name="attribute"></param>
        /// <returns></returns>
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

        internal static int Exit(string msg, ExitCode code)
        {
            Log(msg);
            return (int)code;
        }

        internal static void LogException(Exception e, string msg)
        {
            Console.WriteLine($"{msg}\n{e}");
        }
    }
}
