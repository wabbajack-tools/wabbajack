using System;
using System.Windows.Input;
using ReactiveUI;

namespace Wabbajack
{
    // NOTE: This is a ReactiveUI IBindingTypeConverter (used by ReactiveUI's this.Bind()/OneWayBind()
    // binding pipeline), NOT a XAML System.Windows.Data.IValueConverter / Avalonia.Data.Converters.IValueConverter.
    // Avalonia.ReactiveUI uses the same ReactiveUI binding system (IBindingTypeConverter,
    // BindingTypeConverters registered via Locator.CurrentMutable), so this type ports verbatim -
    // no interface swap to Avalonia.Data.Converters.IValueConverter is needed or appropriate here.
    // System.Windows.Input.ICommand is a BCL/System.ObjectModel type (not PresentationFramework/WPF-only),
    // so it is available unchanged under Avalonia/net10.0-windows.
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
