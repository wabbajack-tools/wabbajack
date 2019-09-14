using System.Dynamic;
using System.Text.RegularExpressions;
using IniParser;
using IniParser.Model;

namespace Wabbajack.Common
{
    public class DynamicIniData : DynamicObject
    {
        private readonly IniData value;

        public DynamicIniData(IniData value) //
        {
            this.value = value;
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
            result = new SectionData(value[binder.Name]);
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
    }
}