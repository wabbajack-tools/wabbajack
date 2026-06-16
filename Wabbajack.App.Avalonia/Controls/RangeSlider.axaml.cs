using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;

namespace Wabbajack;

/// <summary>
/// A dual-thumb range slider mirroring MahApps' RangeSlider used by the WPF gallery: a single
/// track with a lower and upper thumb and a brand-purple fill between them. Exposes
/// <see cref="LowerValue"/>/<see cref="UpperValue"/> against <see cref="Minimum"/>/<see cref="Maximum"/>.
/// </summary>
public class RangeSlider : TemplatedControl
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(Minimum), 0d);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(Maximum), 100d);

    public static readonly StyledProperty<double> LowerValueProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(LowerValue), 0d,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> UpperValueProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(UpperValue), 100d,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double LowerValue { get => GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    public double UpperValue { get => GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }

    private Canvas? _track;
    private Thumb? _lowerThumb;
    private Thumb? _upperThumb;
    private Border? _activeTrack;

    static RangeSlider()
    {
        // Re-layout the thumbs whenever the values or bounds change.
        MinimumProperty.Changed.AddClassHandler<RangeSlider>((s, _) => s.UpdatePositions());
        MaximumProperty.Changed.AddClassHandler<RangeSlider>((s, _) => s.UpdatePositions());
        LowerValueProperty.Changed.AddClassHandler<RangeSlider>((s, _) => s.UpdatePositions());
        UpperValueProperty.Changed.AddClassHandler<RangeSlider>((s, _) => s.UpdatePositions());
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_lowerThumb != null) _lowerThumb.DragDelta -= OnLowerDrag;
        if (_upperThumb != null) _upperThumb.DragDelta -= OnUpperDrag;

        _track = e.NameScope.Find<Canvas>("PART_Track");
        _lowerThumb = e.NameScope.Find<Thumb>("PART_LowerThumb");
        _upperThumb = e.NameScope.Find<Thumb>("PART_UpperThumb");
        _activeTrack = e.NameScope.Find<Border>("PART_ActiveTrack");

        if (_lowerThumb != null) _lowerThumb.DragDelta += OnLowerDrag;
        if (_upperThumb != null) _upperThumb.DragDelta += OnUpperDrag;

        if (_track != null)
            _track.LayoutUpdated += (_, _) => UpdatePositions();

        UpdatePositions();
    }

    private double Range => Math.Max(0.0001, Maximum - Minimum);

    private double TrackWidth => _track?.Bounds.Width ?? 0;

    private void OnLowerDrag(object? sender, VectorEventArgs e)
    {
        if (TrackWidth <= 0) return;
        var delta = e.Vector.X / TrackWidth * Range;
        var next = Math.Clamp(LowerValue + delta, Minimum, UpperValue);
        LowerValue = next;
    }

    private void OnUpperDrag(object? sender, VectorEventArgs e)
    {
        if (TrackWidth <= 0) return;
        var delta = e.Vector.X / TrackWidth * Range;
        var next = Math.Clamp(UpperValue + delta, LowerValue, Maximum);
        UpperValue = next;
    }

    private void UpdatePositions()
    {
        if (_track == null || _lowerThumb == null || _upperThumb == null) return;

        var width = TrackWidth;
        if (width <= 0) return;

        var thumbHalf = _lowerThumb.Bounds.Width / 2;

        var lowerFrac = Math.Clamp((LowerValue - Minimum) / Range, 0, 1);
        var upperFrac = Math.Clamp((UpperValue - Minimum) / Range, 0, 1);

        var lowerX = lowerFrac * width;
        var upperX = upperFrac * width;

        Canvas.SetLeft(_lowerThumb, lowerX - thumbHalf);
        Canvas.SetLeft(_upperThumb, upperX - thumbHalf);

        if (_activeTrack != null)
        {
            _activeTrack.Margin = new Thickness(lowerX, 0, 0, 0);
            _activeTrack.Width = Math.Max(0, upperX - lowerX);
        }
    }
}
