using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using System.Text.RegularExpressions;

namespace Wabbajack.Common
{
    public class DynamicIniData : DynamicObject
    {
        private IniData value;

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

    class SectionData : DynamicObject
    {
        private KeyDataCollection _coll;

        public SectionData(KeyDataCollection coll)
        {
            this._coll = coll;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = _coll[binder.Name];
            if (result is string)
            {
                result = Regex.Unescape(((string)result).Trim('"'));
            }
            return true;
        }
    }

}
