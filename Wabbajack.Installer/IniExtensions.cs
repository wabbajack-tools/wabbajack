using System.IO;
using System.Text;
using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Wabbajack.Paths;

namespace Wabbajack.Installer
{
    public static class IniExtensions
    {
        private static IniDataParser IniParser()
        {
            var config = new IniParserConfiguration { AllowDuplicateKeys = true, AllowDuplicateSections = true };
            var parser = new IniDataParser(config);
            return parser;
        }


        /// <summary>
        ///     Loads INI data from the given filename and returns a dynamic type that
        ///     can use . operators to navigate the INI.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static IniData LoadIniFile(this AbsolutePath file)
        {
            return new FileIniDataParser(IniParser()).ReadFile(file.ToString());
        }

        /// <summary>
        ///     Loads a INI from the given string
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static IniData LoadIniString(this string file)
        {
            return new FileIniDataParser(IniParser()).ReadData(
                new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(file))));
        }
    }
}