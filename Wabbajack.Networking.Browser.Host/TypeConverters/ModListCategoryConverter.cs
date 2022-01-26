using System;
using System.ComponentModel;
using System.Globalization;
using Wabbajack.DTOs.GitHub;

namespace Wabbajack.Networking.Browser.TypeConverters;

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
        return Enum.Parse<List>((string) value);
    }
}