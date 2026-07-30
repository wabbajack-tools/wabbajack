using System;
using System.Windows.Input;
using ReactiveUI;

namespace Wabbajack
{
    // NOTE: This is NOT a XAML IValueConverter/IMultiValueConverter. The WPF source implements
    // ReactiveUI's IBindingTypeConverter, which is registered with Splat's Locator (see
    // ConverterRegistration.cs) and used internally by ReactiveUI's this.Bind(...)/OneWayBind(...)
    // to coerce between IReactiveCommand and ICommand when a view model exposes a ReactiveCommand
    // but the view binds to a plain ICommand-typed property (e.g. Button.Command). This type is
    // part of ReactiveUI core (not a WPF-specific API), and System.Windows.Input.ICommand is the
    // same BCL contract type Avalonia uses for Button.Command, so no Avalonia-specific translation
    // is required here - the class ports verbatim.
    public class CommandConverter : IBindingTypeConverter
    {
        public int GetAffinityForObjects(Type fromType, Type toType)
        {
            if (toType != typeof(ICommand)) return 0;
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
