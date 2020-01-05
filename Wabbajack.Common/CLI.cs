using System;
using System.Linq;
using System.Reflection;

namespace Wabbajack.Common
{
    public static class CLIArguments
    {
        [CLIOptions("nosettings")]
        public static bool NoSettings { get; set; }

        [CLIOptions("apikey")]
        public static string ApiKey { get; set; }

        [CLIOptions("install", ShortOption = 'i')]
        public static string InstallPath { get; set; }
    }

    public static class CLI
    {
        /// <summary>
        /// Parses the argument and sets the properties of <see cref="CLIArguments"/>
        /// </summary>
        /// <param name="args"><see cref="Environment.GetCommandLineArgs"/></param>
        public static void ParseOptions(string[] args)
        {
            if (args.Length == 1) return;
            // get all properties of the class Options
            typeof(CLIArguments).GetProperties().Do(p =>
            {
                var optionAttr = (CLIOptions[])p.GetCustomAttributes(typeof(CLIOptions));
                if (optionAttr.Length != 1)
                    return;

                var cur = optionAttr[0];
                if (cur?.Option == null) return;

                FillVariable(cur.Option, ref p, ref args, false);
                FillVariable(cur.ShortOption, ref p, ref args, true);
            });
        }

        private static void FillVariable(dynamic option, ref PropertyInfo p, ref string[] args, bool single)
        {
            var s = single ? $"-{option}" : $"--{option}";

            if (!args.Any(a => a.Contains(s))) return;

            if (p.PropertyType == typeof(bool))
            {
                p.SetValue(p, true);
                return;
            }

            var filtered = args.Where(a => a.Contains(s)).ToList();
            if (filtered.Count != 1) return;

            var arg = filtered[0];
            arg = arg.Replace($"{s}=", "");

            if(p.PropertyType == typeof(string))
                p.SetValue(p, arg);
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class CLIOptions : Attribute
    {
        // --option, long name of the option. Eg: --output
        public string Option;
        // -shortOption, short name of the option. Eg: -o
        public char ShortOption;

        public CLIOptions(string option)
        {
            Option = option;
        }
    }
}
