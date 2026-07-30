using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;

namespace Wabbajack;

/// <summary>
/// Avalonia replacement for MahApps.Metro's dual-thumb RangeSlider, preserving the property surface
/// the gallery size filter binds to. Renders a single track with a lower and an upper thumb and a
/// highlighted selected range between them.
/// </summary>
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

    private Canvas? _track;
    private Thumb? _lowerThumb;
    private Thumb? _upperThumb;
    private Border? _rangeBar;

    static RangeSlider()
    {
        // Any of these changing needs the thumbs/range bar repositioned.
        LowerValueProperty.Changed.AddClassHandler<RangeSlider>((c, _) => c.UpdatePositions());
        UpperValueProperty.Changed.AddClassHandler<RangeSlider>((c, _) => c.UpdatePositions());
        MinimumProperty.Changed.AddClassHandler<RangeSlider>((c, _) => c.UpdatePositions());
        MaximumProperty.Changed.AddClassHandler<RangeSlider>((c, _) => c.UpdatePositions());
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _track = e.NameScope.Find<Canvas>("PART_Track");
        _lowerThumb = e.NameScope.Find<Thumb>("PART_LowerThumb");
        _upperThumb = e.NameScope.Find<Thumb>("PART_UpperThumb");
        _rangeBar = e.NameScope.Find<Border>("PART_RangeBar");

        if (_lowerThumb is not null) _lowerThumb.DragDelta += (_, args) => OnThumbDrag(args.Vector.X, isLower: true);
        if (_upperThumb is not null) _upperThumb.DragDelta += (_, args) => OnThumbDrag(args.Vector.X, isLower: false);
        if (_track is not null) _track.PropertyChanged += (_, args) =>
        {
            if (args.Property == BoundsProperty) UpdatePositions();
        };

        UpdatePositions();
    }

    private double TrackWidth => Math.Max(0, (_track?.Bounds.Width ?? 0) - ThumbSize);
    private const double ThumbSize = 16;

    private void OnThumbDrag(double deltaX, bool isLower)
    {
        var span = Maximum - Minimum;
        if (span <= 0 || TrackWidth <= 0) return;

        var valueDelta = deltaX / TrackWidth * span;
        if (isLower)
        {
            var v = Math.Clamp(LowerValue + valueDelta, Minimum, Math.Max(Minimum, UpperValue - MinRange));
            LowerValue = v;
        }
        else
        {
            var v = Math.Clamp(UpperValue + valueDelta, Math.Min(Maximum, LowerValue + MinRange), Maximum);
            UpperValue = v;
        }
    }

    private void UpdatePositions()
    {
        if (_track is null) return;

        var span = Maximum - Minimum;
        if (span <= 0) return;

        var lower = Math.Clamp((LowerValue - Minimum) / span, 0, 1) * TrackWidth;
        var upper = Math.Clamp((UpperValue - Minimum) / span, 0, 1) * TrackWidth;

        if (_lowerThumb is not null) Canvas.SetLeft(_lowerThumb, lower);
        if (_upperThumb is not null) Canvas.SetLeft(_upperThumb, upper);

        if (_rangeBar is not null)
        {
            Canvas.SetLeft(_rangeBar, lower + ThumbSize / 2);
            _rangeBar.Width = Math.Max(0, upper - lower);
        }
    }
}
