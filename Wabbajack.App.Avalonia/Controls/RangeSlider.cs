using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;

namespace Wabbajack;

// Minimal Avalonia replacement for MahApps.Metro's dual-thumb RangeSlider, preserving the property
// surface the gallery size filter binds to.
// TODO(avalonia-control): give this a real dual-thumb template/interaction in the theme phase.
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
    public static readonly StyledProperty<double> MinRangeProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(MinRange));
    public static readonly StyledProperty<int> AutoToolTipPrecisionProperty =
        AvaloniaProperty.Register<RangeSlider, int>(nameof(AutoToolTipPrecision));
    public static readonly StyledProperty<string?> AutoToolTipPlacementProperty =
        AvaloniaProperty.Register<RangeSlider, string?>(nameof(AutoToolTipPlacement));
    public static readonly StyledProperty<IBrush?> ThumbFillBrushProperty =
        AvaloniaProperty.Register<RangeSlider, IBrush?>(nameof(ThumbFillBrush));
    public static readonly StyledProperty<IBrush?> ThumbFillHoverBrushProperty =
        AvaloniaProperty.Register<RangeSlider, IBrush?>(nameof(ThumbFillHoverBrush));
    public static readonly StyledProperty<IBrush?> ThumbFillPressedBrushProperty =
        AvaloniaProperty.Register<RangeSlider, IBrush?>(nameof(ThumbFillPressedBrush));
    public static readonly StyledProperty<IBrush?> TrackValueFillBrushProperty =
        AvaloniaProperty.Register<RangeSlider, IBrush?>(nameof(TrackValueFillBrush));

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double LowerValue { get => GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    public double UpperValue { get => GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }
    public double MinRange { get => GetValue(MinRangeProperty); set => SetValue(MinRangeProperty, value); }
    public int AutoToolTipPrecision { get => GetValue(AutoToolTipPrecisionProperty); set => SetValue(AutoToolTipPrecisionProperty, value); }
    public string? AutoToolTipPlacement { get => GetValue(AutoToolTipPlacementProperty); set => SetValue(AutoToolTipPlacementProperty, value); }
    public IBrush? ThumbFillBrush { get => GetValue(ThumbFillBrushProperty); set => SetValue(ThumbFillBrushProperty, value); }
    public IBrush? ThumbFillHoverBrush { get => GetValue(ThumbFillHoverBrushProperty); set => SetValue(ThumbFillHoverBrushProperty, value); }
    public IBrush? ThumbFillPressedBrush { get => GetValue(ThumbFillPressedBrushProperty); set => SetValue(ThumbFillPressedBrushProperty, value); }
    public IBrush? TrackValueFillBrush { get => GetValue(TrackValueFillBrushProperty); set => SetValue(TrackValueFillBrushProperty, value); }
}
