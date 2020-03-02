using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using System.Text.RegularExpressions;
using IniParser;
using IniParser.Exceptions;
using IniParser.Model;

namespace Wabbajack.Common
{
    public class DynamicIniData : DynamicObject
    {
        private readonly IniData _value;

        public DynamicIniData(IniData value) //
        {
            _value = value;
        }

        public static dynamic FromIni(IniData data)
        {
            return new DynamicIniData(data);
        }

        public static dynamic FromFile(string filename)
        {
            var fi = new FileIniDataParser();
            return new DynamicIniData(fi.ReadFile(filename));
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = new SectionData(_value[binder.Name]);
            return true;
        }
        
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (indexes.Length > 1)
            {
                result = null;
                return false;
            }

            result = new SectionData(_value[(string) indexes[0]]);
            return true;
        }
    }

    public class SectionData : DynamicObject
    {
        public KeyDataCollection Coll { get; }

        public SectionData(KeyDataCollection coll)
        {
            Coll = coll;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = Coll[binder.Name];
            if (result is string s) result = Interpret(s);
            return true;
        }

        private static string Interpret(string s)
        {
            if (s.StartsWith("@ByteArray(") && s.EndsWith(")"))
            {
                return UnescapeUTF8(s.Substring("@ByteArray(".Length, s.Length - "@ByteArray(".Length - ")".Length));
            }

            return UnescapeString(s);
        }

        private static string UnescapeString(string s)
        {
            return Regex.Unescape(s.Trim('"'));
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
                                acc.Add((byte)'\\');
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
                        acc.Add((byte)c);
                        break;
                }
            }
            return Encoding.UTF8.GetString(acc.ToArray());
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (indexes.Length > 1)
            {
                result = null;
                return false;
            }

            result = Coll[(string) indexes[0]];
            if (result is string s) result = Regex.Unescape(s.Trim('"'));
            return true;
        }
    }
}
