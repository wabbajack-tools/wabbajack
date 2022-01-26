using System;
using System.ComponentModel;
using System.Globalization;
using Wabbajack.Paths;

namespace Wabbajack.CLI.TypeConverters;

public class AbsolutePathTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value,
        Type destinationType)
    {
        return (AbsolutePath) (string) value;
    }

    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        return (AbsolutePath) (string) value;
    }
}