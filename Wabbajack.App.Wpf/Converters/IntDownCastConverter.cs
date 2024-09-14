using System;
using System.Windows.Input;
using ReactiveUI;

namespace Wabbajack
{
    public class IntDownCastConverter : IBindingTypeConverter
    {
        public int GetAffinityForObjects(Type fromType, Type toType)
        {
            if (toType == typeof(int) || fromType == typeof(int?)) return 1;
            if (fromType == typeof(ICommand)
                || fromType == typeof(IReactiveCommand))
            {
                return 1;
            }
            return 0;
        }

        public bool TryConvert(object from, Type toType, object conversionHint, out object result)
        {
            if (from == null)
            {
                result = default(ICommand);
                return true;
            }
            result = from as ICommand;
            return result != null;
        }
    }
}
