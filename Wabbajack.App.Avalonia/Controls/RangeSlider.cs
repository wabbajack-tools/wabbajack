using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Wabbajack;

// Minimal Avalonia replacement for MahApps.Metro's dual-thumb RangeSlider, preserving the
// Minimum/Maximum/LowerValue/UpperValue property surface the gallery size filter binds to.
// TODO(avalonia-control): give this a real dual-thumb template/interaction.
public class RangeSlider : TemplatedControl
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(Maximum), 100d);

    public static readonly StyledProperty<double> LowerValueProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(LowerValue), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> UpperValueProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(UpperValue), 100d, defaultBindingMode: BindingMode.TwoWay);

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double LowerValue { get => GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    public double UpperValue { get => GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }
}
