
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ValveKeyValue;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Services;

internal static class WriterHelpers
{
    internal static void AddValue<TValue>(this List<KVObject> list, string name, TValue value, TValue defaultValue)
        where TValue : notnull
    {
        if (value.Equals(defaultValue)) return;
        list.Add(new KVObject(name, new StringValue(value)));
    }

    internal static void AddDictionary<TKey, TValue>(this List<KVObject> list, string name, IReadOnlyDictionary<TKey, TValue> dictionary, TValue defaultValue)
        where TKey : notnull
        where TValue : notnull
    {
        if (dictionary.Count == 0) return;

        var children = new List<KVObject>();
        foreach (var kv in dictionary)
        {
            children.AddValue(kv.Key.ToString()!, kv.Value, defaultValue);
        }

        list.Add(new KVObject(name, children));
    }

    [ExcludeFromCodeCoverage]
    private class StringValue : KVValue
    {
        private readonly string _value;

        public StringValue(string value)
        {
            _value = value;
        }

        public StringValue(object obj)
        {
            _value = obj.ToString() ?? throw new ArgumentException($"Doesn't have a ToString: {obj.GetType()}", nameof(obj));
        }

        public override string ToString() => _value;

        public override KVValueType ValueType => KVValueType.String;
        public override TypeCode GetTypeCode() => TypeCode.String;

        public override bool ToBoolean(IFormatProvider? provider) => Convert.ToBoolean(_value, provider);
        public override byte ToByte(IFormatProvider? provider) => Convert.ToByte(_value, provider);
        public override char ToChar(IFormatProvider? provider) => Convert.ToChar(_value, provider);
        public override DateTime ToDateTime(IFormatProvider? provider) => Convert.ToDateTime(_value, provider);
        public override decimal ToDecimal(IFormatProvider? provider) => Convert.ToDecimal(_value, provider);
        public override double ToDouble(IFormatProvider? provider) => Convert.ToDouble(_value, provider);
        public override short ToInt16(IFormatProvider? provider) => Convert.ToInt16(_value, provider);
        public override int ToInt32(IFormatProvider? provider) => Convert.ToInt32(_value, provider);
        public override long ToInt64(IFormatProvider? provider) => Convert.ToInt64(_value, provider);
        public override sbyte ToSByte(IFormatProvider? provider) => Convert.ToSByte(_value, provider);
        public override float ToSingle(IFormatProvider? provider) => Convert.ToSingle(_value, provider);
        public override string ToString(IFormatProvider? provider) => Convert.ToString(_value, provider);
        public override object ToType(Type conversionType, IFormatProvider? provider) => throw new NotSupportedException();
        public override ushort ToUInt16(IFormatProvider? provider) => Convert.ToUInt16(_value, provider);
        public override uint ToUInt32(IFormatProvider? provider) => Convert.ToUInt32(_value, provider);
        public override ulong ToUInt64(IFormatProvider? provider) => Convert.ToUInt64(_value, provider);
    }
}
