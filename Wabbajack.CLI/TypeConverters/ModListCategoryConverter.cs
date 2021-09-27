using System;
using System.ComponentModel;
using System.Globalization;
using Wabbajack.DTOs.GitHub;
using Wabbajack.Paths;

namespace Wabbajack.CLI.TypeConverters
{
    public class ModListCategoryConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value,
            Type destinationType)
        {
            throw new NotImplementedException();
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            return Enum.Parse<List>((string)value);
        }
    }
}