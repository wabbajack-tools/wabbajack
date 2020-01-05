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
    }

    public static class CLI
    {
        /// <summary>
        /// Parses the argument and sets the properties of <see cref="CLIArguments"/>
        /// </summary>
        /// <param name="args"><see cref="Environment.GetCommandLineArgs"/></param>
        public static void ParseOptions(string[] args)
        {
            if (args.Length == 0) return;
            // get all properties of the class Options
            typeof(CLIArguments).GetProperties().Do(p =>
            {
                var optionAttr = (CLIOptions[])p.GetCustomAttributes(typeof(CLIOptions));
                if (optionAttr.Length != 1)
                    return;

                var cur = optionAttr[0];
                if (cur == null) return;
                
                if (cur.Option != null && args.Any(a => a.Contains($"--{cur.Option}")))
                {
                    if (p.PropertyType == typeof(bool))
                    {
                        p.SetValue(p, true);
                        return;
                    }

                    // filter to get the actual argument
                    var filtered = args.Where(a => a.Contains($"--{cur.Option}")).ToList();
                    if (filtered.Count != 1) return;

                    // eg: --apikey="something"
                    var arg = filtered[0];
                    arg = arg.Replace($"--{cur.Option}=", "");

                    // prev: --apikey="something", result: something
                    if (p.PropertyType == typeof(string))
                        p.SetValue(p, arg);
                }

                if (cur.ShortOption == 0) return;
            });
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
