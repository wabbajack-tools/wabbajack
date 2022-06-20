using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using IniParser;
using IniParser.Exceptions;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Wabbajack.Paths;

namespace Wabbajack.Installer;

public static class IniExtensions
{
    private static IniDataParser IniParser()
    {
        var config = new IniParserConfiguration {AllowDuplicateKeys = true, AllowDuplicateSections = true};
        var parser = new IniDataParser(config);
        return parser;
    }


    /// <summary>
    ///     Loads INI data from the given filename and returns a dynamic type that
    ///     can use . operators to navigate the INI.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public static IniData  LoadIniFile(this AbsolutePath file)
    {
        return new FileIniDataParser(IniParser()).ReadFile(file.ToString());
    }

    public static void SaveIniFile(this IniData data, AbsolutePath file)
    {
        var parser = new FileIniDataParser(IniParser());
        parser.WriteFile(file.ToString(), data);
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

    public static string FromMO2Ini(this string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        if (s.StartsWith("@ByteArray(") && s.EndsWith(")"))
            return UnescapeUTF8(s.Substring("@ByteArray(".Length, s.Length - "@ByteArray(".Length - ")".Length));

        return UnescapeString(s);
    }

    private static string UnescapeString(string s)
    {
        if (s.Trim().StartsWith("\"") || s.Contains("\\\\"))
            return Regex.Unescape(s.Trim('"'));
        return s;
    }

    private static string UnescapeUTF8(string s)
    {
        var acc = new List<byte>();
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            switch (c)
            {
                case '\\':
                    i++;
                    var nc = s[i];
                    switch (nc)
                    {
                        case '\\':
                            acc.Add((byte) '\\');
                            break;
                        case 'x':
                            var chrs = s[i + 1] + s[i + 2].ToString();
                            i += 2;
                            acc.Add(Convert.ToByte(chrs, 16));
                            break;
                        default:
                            throw new ParsingException($"Not a valid escape characer {nc}");
                    }

                    break;
                default:
                    acc.Add((byte) c);
                    break;
            }
        }

        return Encoding.UTF8.GetString(acc.ToArray());
    }
}