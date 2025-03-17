using System;
using ReactiveUI;
using Wabbajack.RateLimiter;

namespace Wabbajack
{
    public class PercentToDoubleConverter : IBindingTypeConverter
    {
        public int GetAffinityForObjects(Type fromType, Type toType)
        {
            if (toType == typeof(double)) return 1;
            if (toType == typeof(double?)) return 1;
            if (toType == typeof(Percent)) return 1;
            if (toType == typeof(Percent?)) return 1;
            return 0;
        }

        public bool TryConvert(object from, Type toType, object conversionHint, out object result)
        {
            if (toType == typeof(double))
            {
                if (from is Percent p)
                {
                    result = p.Value;
                    return true;
                }
                result = 0d;
                return false;
            }
            if (toType == typeof(double?))
            {
                if (from is Percent p)
                {
                    result = p.Value;
                    return true;
                }
                if (from == null)
                {
                    result = default(double?);
                    return true;
                }
                result = default(double?);
                return false;
            }
            if (toType == typeof(Percent))
            {
                if (from is double d)
                {
                    result = Percent.FactoryPutInRange(d);
                    return true;
                }
                result = Percent.Zero;
                return false;
            }
            if (toType == typeof(Percent?))
            {
                if (from is double d)
                {
                    result = Percent.FactoryPutInRange(d);
                    return true;
                }
                if (from == null)
                {
                    result = default(Percent?);
                    return true;
                }
                result = Percent.Zero;
                return false;
            }

            result = null;
            return false;
        }
    }
}
