using System.Dynamic;
using System.Text.RegularExpressions;
using IniParser;
using IniParser.Model;

namespace Wabbajack.Common
{
    public class DynamicIniData : DynamicObject
    {
        private readonly IniData _value;

        public DynamicIniData(IniData value) //
        {
            this._value = value;
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
    }

    internal class SectionData : DynamicObject
    {
        private readonly KeyDataCollection _coll;

        public SectionData(KeyDataCollection coll)
        {
            _coll = coll;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = _coll[binder.Name];
            if (result is string) result = Regex.Unescape(((string) result).Trim('"'));
            return true;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (indexes.Length > 1)
            {
                result = null;
                return false;
            }

            result = _coll[(string) indexes[0]];
            if (result is string) result = Regex.Unescape(((string)result).Trim('"'));
            return true;
        }
    }
}