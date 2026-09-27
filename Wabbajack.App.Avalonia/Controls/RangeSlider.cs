using System;
using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Wabbajack.App.Avalonia.Controls;

/// <summary>Where <see cref="RangeSlider" /> shows the value it is dragging. MahApps' AutoToolTipPlacement.</summary>
public enum RangeSliderToolTipPlacement
{
    None,
    TopLeft,
    BottomRight
}

/// <summary>
/// MahApps' RangeSlider, horizontal only: two thumbs picking a <see cref="LowerValue" /> and an
/// <see cref="UpperValue" /> between <see cref="Minimum" /> and <see cref="Maximum" />, and the bar between
/// them, which drags both at once. The look is in Themes/RangeSlider.axaml.
/// <para>
/// The arithmetic is MahApps' own (3.0.0-alpha0476, the version the WPF app ships), kept in pixels the way it
/// keeps it: the two track pieces and the middle bar are sized from the values, a drag resizes them, and the
/// values are read back off the new widths. Doing it in value space instead would round differently at the
/// ends, and <see cref="MinRangeWidth" /> - a width in pixels the middle bar never shrinks below, which is not
/// a constraint on the values at all - only makes sense in pixels.
/// </para>
/// <para>
/// Left out, because the gallery never used them: vertical orientation, ticks and snapping, ExtendedMode,
/// IsMoveToPointEnabled and the keyboard commands.
/// </para>
/// </summary>
[TemplatePart("PART_RangeSliderContainer", typeof(Panel))]
[TemplatePart("PART_LeftEdge", typeof(Control))]
[TemplatePart("PART_LeftThumb", typeof(Thumb))]
[TemplatePart("PART_MiddleThumb", typeof(Thumb))]
[TemplatePart("PART_RightThumb", typeof(Thumb))]
[TemplatePart("PART_RightEdge", typeof(Control))]
[TemplatePart("PART_AutoToolTip", typeof(Popup))]
[TemplatePart("PART_AutoToolTipText", typeof(TextBlock))]
[PseudoClasses(":active")]
public class RangeSlider : TemplatedControl
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(Minimum), 0d, coerce: CoerceMinimum);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(Maximum), 100d, coerce: CoerceMaximum);

    public static readonly StyledProperty<double> LowerValueProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(LowerValue), 0d,
            defaultBindingMode: BindingMode.TwoWay, coerce: CoerceLowerValue);

    public static readonly StyledProperty<double> UpperValueProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(UpperValue), 0d,
            defaultBindingMode: BindingMode.TwoWay, coerce: CoerceUpperValue);

    /// <summary>The smallest distance, in values, between the two. MahApps' default of 0 lets them meet.</summary>
    public static readonly StyledProperty<double> MinRangeProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(MinRange), 0d, validate: v => v >= 0,
            coerce: CoerceMinRange);

    /// <summary>The narrowest the middle bar is drawn, in pixels. MahApps' default is 30.</summary>
    public static readonly StyledProperty<double> MinRangeWidthProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(MinRangeWidth), 30d, validate: v => v >= 0,
            coerce: CoerceMinRangeWidth);

    public static readonly StyledProperty<double> SmallChangeProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(SmallChange), 0.1);

    public static readonly StyledProperty<double> LargeChangeProperty =
        AvaloniaProperty.Register<RangeSlider, double>(nameof(LargeChange), 1d);

    /// <summary>Milliseconds between steps while the track is held down.</summary>
    public static readonly StyledProperty<int> IntervalProperty =
        AvaloniaProperty.Register<RangeSlider, int>(nameof(Interval), 100, validate: v => v >= 0);

    /// <summary>Whether holding the track moves the whole range rather than the nearer end. A middle click toggles it.</summary>
    public static readonly StyledProperty<bool> MoveWholeRangeProperty =
        AvaloniaProperty.Register<RangeSlider, bool>(nameof(MoveWholeRange));

    public static readonly StyledProperty<RangeSliderToolTipPlacement> AutoToolTipPlacementProperty =
        AvaloniaProperty.Register<RangeSlider, RangeSliderToolTipPlacement>(nameof(AutoToolTipPlacement));

    /// <summary>Decimal places in the drag tooltip.</summary>
    public static readonly StyledProperty<int> AutoToolTipPrecisionProperty =
        AvaloniaProperty.Register<RangeSlider, int>(nameof(AutoToolTipPrecision), validate: v => v >= 0);

    // WPF's ToolTip template faded its border in over 0.3s when it opened.
    private static readonly Animation ToolTipFadeIn = new()
    {
        Duration = TimeSpan.FromSeconds(0.3),
        Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, 0d) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, 1d) } }
        }
    };

    private readonly DispatcherTimer _timer;

    private Panel? _container;
    private Control? _leftEdge;
    private Control? _rightEdge;
    private Thumb? _leftThumb;
    private Thumb? _middleThumb;
    private Thumb? _rightThumb;
    private Popup? _toolTip;
    private TextBlock? _toolTipText;

    private double _movableWidth;
    private double _density;
    private bool _internalUpdate;

    // MahApps rounds a value stepped by holding the track to the step's precision, but only until the first
    // time anything is dragged: its _isMoved is set on every drag start and never cleared. Kept as it is.
    private bool _isMoved;
    private bool _roundToPrecision;
    private int _precision;

    private Direction _direction;
    private ButtonType _buttonType;
    private bool _isInsideRange;
    private int _tickCount;
    private double _pointerX;
    private bool _trackHeld;
    private Thumb? _dragging;

    public RangeSlider()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Interval) };
        _timer.Tick += MoveToNextValue;
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double LowerValue
    {
        get => GetValue(LowerValueProperty);
        set => SetValue(LowerValueProperty, value);
    }

    public double UpperValue
    {
        get => GetValue(UpperValueProperty);
        set => SetValue(UpperValueProperty, value);
    }

    public double MinRange
    {
        get => GetValue(MinRangeProperty);
        set => SetValue(MinRangeProperty, value);
    }

    public double MinRangeWidth
    {
        get => GetValue(MinRangeWidthProperty);
        set => SetValue(MinRangeWidthProperty, value);
    }

    public double SmallChange
    {
        get => GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    public double LargeChange
    {
        get => GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    public int Interval
    {
        get => GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    public bool MoveWholeRange
    {
        get => GetValue(MoveWholeRangeProperty);
        set => SetValue(MoveWholeRangeProperty, value);
    }

    public RangeSliderToolTipPlacement AutoToolTipPlacement
    {
        get => GetValue(AutoToolTipPlacementProperty);
        set => SetValue(AutoToolTipPlacementProperty, value);
    }

    public int AutoToolTipPrecision
    {
        get => GetValue(AutoToolTipPrecisionProperty);
        set => SetValue(AutoToolTipPrecisionProperty, value);
    }

    private double MovableRange => Maximum - Minimum - MinRange;

    #region Coercion

    private static double CoerceMinimum(AvaloniaObject o, double value)
    {
        var s = (RangeSlider)o;
        return value > s.Maximum ? s.Maximum : value;
    }

    private static double CoerceMaximum(AvaloniaObject o, double value)
    {
        var s = (RangeSlider)o;
        return value < s.Minimum ? s.Minimum : value;
    }

    private static double CoerceLowerValue(AvaloniaObject o, double value)
    {
        var s = (RangeSlider)o;
        if (value < s.Minimum || s.UpperValue - s.MinRange < s.Minimum)
            return s.Minimum;
        if (value > s.UpperValue - s.MinRange)
            return s.UpperValue - s.MinRange;
        return value;
    }

    private static double CoerceUpperValue(AvaloniaObject o, double value)
    {
        var s = (RangeSlider)o;
        if (value > s.Maximum || s.LowerValue + s.MinRange > s.Maximum)
            return s.Maximum;
        if (value < s.LowerValue + s.MinRange)
            return s.LowerValue + s.MinRange;
        return value;
    }

    private static double CoerceMinRange(AvaloniaObject o, double value)
    {
        var s = (RangeSlider)o;
        return s.LowerValue + value > s.Maximum ? s.Maximum - s.LowerValue : value;
    }

    // MahApps caps the bar at half the track, but only once there is a track to measure; before layout it
    // leaves the value alone rather than capping it against a width of nothing.
    private static double CoerceMinRangeWidth(AvaloniaObject o, double value)
    {
        var s = (RangeSlider)o;
        if (s._leftThumb == null || s._rightThumb == null || s.Bounds.Width <= 0)
            return value;
        var width = s.Bounds.Width - ThumbWidth(s._leftThumb) - ThumbWidth(s._rightThumb);
        return value > width / 2 ? width / 2 : value;
    }

    private void CoerceLowerUpperValues()
    {
        CoerceValue(LowerValueProperty);
        CoerceValue(UpperValueProperty);
        ReCalculateSize();
    }

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LowerValueProperty || change.Property == UpperValueProperty)
        {
            if (!_internalUpdate)
                CoerceLowerUpperValues();
        }
        else if (change.Property == MinimumProperty)
        {
            CoerceValue(MaximumProperty);
            CoerceValue(LowerValueProperty);
            ReCalculateSize();
        }
        else if (change.Property == MaximumProperty)
        {
            CoerceValue(MinimumProperty);
            CoerceValue(UpperValueProperty);
            ReCalculateSize();
        }
        else if (change.Property == MinRangeProperty)
        {
            // MahApps pushes the upper value out to keep the new distance, then settles both.
            _internalUpdate = true;
            SetCurrentValue(UpperValueProperty, Math.Min(Math.Max(UpperValue, LowerValue + MinRange), Maximum));
            _internalUpdate = false;
            CoerceValue(UpperValueProperty);
            ReCalculateSize();
        }
        else if (change.Property == MinRangeWidthProperty || change.Property == BoundsProperty ||
                 change.Property == IsVisibleProperty)
        {
            ReCalculateSize();
        }
        else if (change.Property == IntervalProperty)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(Interval);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_container != null)
        {
            _container.RemoveHandler(PointerPressedEvent, ContainerPointerPressed);
            _container.RemoveHandler(PointerReleasedEvent, ContainerPointerReleased);
            _container.RemoveHandler(PointerMovedEvent, ContainerPointerMoved);
            _container.PointerExited -= ContainerPointerExited;
        }
        foreach (var thumb in new[] { _leftThumb, _middleThumb, _rightThumb })
        {
            if (thumb == null) continue;
            thumb.DragStarted -= ThumbDragStarted;
            thumb.DragDelta -= ThumbDragDelta;
            thumb.DragCompleted -= ThumbDragCompleted;
        }

        _container = e.NameScope.Get<Panel>("PART_RangeSliderContainer");
        _leftEdge = e.NameScope.Get<Control>("PART_LeftEdge");
        _rightEdge = e.NameScope.Get<Control>("PART_RightEdge");
        _leftThumb = e.NameScope.Get<Thumb>("PART_LeftThumb");
        _middleThumb = e.NameScope.Get<Thumb>("PART_MiddleThumb");
        _rightThumb = e.NameScope.Get<Thumb>("PART_RightThumb");
        _toolTip = e.NameScope.Find<Popup>("PART_AutoToolTip");
        _toolTipText = e.NameScope.Find<TextBlock>("PART_AutoToolTipText");

        // Tunnelling, as MahApps' PreviewMouseDown is: the track is pressed before anything inside it hears.
        _container.AddHandler(PointerPressedEvent, ContainerPointerPressed, RoutingStrategies.Tunnel);
        _container.AddHandler(PointerReleasedEvent, ContainerPointerReleased, RoutingStrategies.Tunnel, true);
        _container.AddHandler(PointerMovedEvent, ContainerPointerMoved, RoutingStrategies.Tunnel, true);
        _container.PointerExited += ContainerPointerExited;

        foreach (var thumb in new[] { _leftThumb, _middleThumb, _rightThumb })
        {
            thumb.DragStarted += ThumbDragStarted;
            thumb.DragDelta += ThumbDragDelta;
            thumb.DragCompleted += ThumbDragCompleted;
        }

        ReCalculateSize();
    }

    #region Layout

    private static double ThumbWidth(Control thumb) =>
        thumb.Bounds.Width > 0 ? thumb.Bounds.Width : double.IsNaN(thumb.Width) ? 0 : thumb.Width;

    // MahApps' ReCalculateSize: the track pieces take the share of the movable width their values are worth,
    // and the middle bar takes what is left, which is never less than MinRangeWidth.
    private void ReCalculateSize()
    {
        if (_leftEdge == null || _rightEdge == null || _middleThumb == null || _leftThumb == null ||
            _rightThumb == null)
            return;

        var width = Bounds.Width;
        var thumbs = ThumbWidth(_leftThumb) + ThumbWidth(_rightThumb);
        _movableWidth = Math.Max(width - thumbs - MinRangeWidth, 1);

        if (MovableRange <= 0)
        {
            _leftEdge.Width = double.NaN;
            _rightEdge.Width = double.NaN;
        }
        else
        {
            _leftEdge.Width = Math.Max(_movableWidth * (LowerValue - Minimum) / MovableRange, 0);
            _rightEdge.Width = Math.Max(_movableWidth * (Maximum - UpperValue) / MovableRange, 0);
        }

        _middleThumb.Width = IsValid(_leftEdge.Width) && IsValid(_rightEdge.Width)
            ? Math.Max(width - (_leftEdge.Width + _rightEdge.Width + thumbs), 0)
            : Math.Max(width - thumbs, 0);

        _density = _movableWidth / MovableRange;
    }

    // MahApps' MoveThumbHorizontal: grows one element and shrinks its neighbour by the same amount, never
    // taking a width below zero, nor the middle bar below its MinWidth.
    private void MoveThumb(Control x, Control y, double change)
    {
        _direction = change < 0 ? Direction.Decrease : Direction.Increase;
        if (!IsValid(x.Width) || !IsValid(y.Width))
            return;

        if (change < 0)
        {
            var c = KeepPositive(x.Width, change);
            if (x == _middleThumb)
            {
                if (x.Width > x.MinWidth)
                {
                    if (x.Width + c < x.MinWidth)
                    {
                        var dif = x.Width - x.MinWidth;
                        x.Width = x.MinWidth;
                        y.Width += dif;
                    }
                    else
                    {
                        x.Width += c;
                        y.Width -= c;
                    }
                }
            }
            else
            {
                x.Width += c;
                y.Width -= c;
            }
        }
        else if (change > 0)
        {
            var c = -KeepPositive(y.Width, -change);
            if (y == _middleThumb)
            {
                if (y.Width > y.MinWidth)
                {
                    if (y.Width - c < y.MinWidth)
                    {
                        var dif = y.Width - y.MinWidth;
                        y.Width = y.MinWidth;
                        x.Width += dif;
                    }
                    else
                    {
                        x.Width += c;
                        y.Width -= c;
                    }
                }
            }
            else
            {
                x.Width += c;
                y.Width -= c;
            }
        }
    }

    private static double KeepPositive(double width, double increment) => Math.Max(width + increment, 0) - width;

    // MahApps' ReCalculateRangeSelected: reads the values back off the track pieces' new widths, pinning a
    // piece of width zero to the exact end so a drag to the edge lands on Minimum or Maximum.
    private void ReCalculateRangeSelected(bool lower, bool upper)
    {
        _internalUpdate = true;
        if (_direction == Direction.Increase)
        {
            if (upper) ReadUpper();
            if (lower) ReadLower();
        }
        else
        {
            if (lower) ReadLower();
            if (upper) ReadUpper();
        }
        _roundToPrecision = false;
        _internalUpdate = false;
    }

    private void ReadLower()
    {
        var width = _leftEdge!.Width;
        if (!IsValid(width)) return;
        var value = width == 0 ? Minimum : Math.Max(Minimum, Minimum + MovableRange * width / _movableWidth);
        SetCurrentValue(LowerValueProperty, Round(value));
    }

    private void ReadUpper()
    {
        var width = _rightEdge!.Width;
        if (!IsValid(width)) return;
        var value = width == 0 ? Maximum : Math.Min(Maximum, Maximum - MovableRange * width / _movableWidth);
        SetCurrentValue(UpperValueProperty, Round(value));
    }

    private double Round(double value) =>
        !_isMoved && _roundToPrecision ? Math.Round(value, _precision) : value;

    private static bool IsValid(double d) => !double.IsNaN(d) && !double.IsInfinity(d);

    #endregion

    #region Dragging

    private void ThumbDragStarted(object? sender, VectorEventArgs e)
    {
        _isMoved = true;
        _dragging = sender as Thumb;
        UpdateActive();
        ShowToolTip();
    }

    private void ThumbDragDelta(object? sender, VectorEventArgs e)
    {
        var change = e.Vector.X;
        if (sender == _leftThumb)
        {
            MoveThumb(_leftEdge!, _middleThumb!, change);
            ReCalculateRangeSelected(true, false);
        }
        else if (sender == _rightThumb)
        {
            MoveThumb(_middleThumb!, _rightEdge!, change);
            ReCalculateRangeSelected(false, true);
        }
        else
        {
            MoveThumb(_leftEdge!, _rightEdge!, change);
            ReCalculateRangeSelected(true, true);
        }
        CoerceLowerUpperValues();
        UpdateToolTipText();
    }

    private void ThumbDragCompleted(object? sender, VectorEventArgs e)
    {
        _dragging = null;
        UpdateActive();
        if (_toolTip != null)
            _toolTip.IsOpen = false;
    }

    #endregion

    #region Holding the track

    // MahApps' VisualElementsContainerPreviewMouseDown: a press left of the lower thumb or right of the upper
    // one starts a timer that steps that end (or the whole range) toward the pointer until it gets there.
    private void ContainerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_container);
        _pointerX = point.Position.X;

        if (point.Properties.IsMiddleButtonPressed)
        {
            SetCurrentValue(MoveWholeRangeProperty, !MoveWholeRange);
            return;
        }
        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (_pointerX < _leftEdge!.Bounds.Width)
            StartStepping(MoveWholeRange ? ButtonType.Both : ButtonType.BottomLeft, Direction.Decrease);
        else if (_pointerX > Bounds.Width - _rightEdge!.Bounds.Width)
            StartStepping(MoveWholeRange ? ButtonType.Both : ButtonType.TopRight, Direction.Increase);
    }

    private void StartStepping(ButtonType type, Direction direction)
    {
        _buttonType = type;
        _direction = direction;
        _isInsideRange = false;
        _trackHeld = true;
        UpdateActive();
        _timer.Start();
    }

    private void ContainerPointerMoved(object? sender, PointerEventArgs e) =>
        _pointerX = e.GetPosition(_container).X;

    private void ContainerPointerReleased(object? sender, PointerReleasedEventArgs e) => StopStepping();

    private void ContainerPointerExited(object? sender, PointerEventArgs e) => StopStepping();

    private void StopStepping()
    {
        _tickCount = 0;
        _timer.Stop();
        if (!_trackHeld) return;
        _trackHeld = false;
        UpdateActive();
    }

    // MahApps' MoveToNextValue: SmallChange for the first six steps, LargeChange after, and only while the
    // pointer is still beyond the end being moved.
    private void MoveToNextValue(object? sender, EventArgs e)
    {
        var endpoint = EndPoint(_buttonType, _direction);
        var beyond = _direction == Direction.Increase ? _pointerX > endpoint : _pointerX < endpoint;

        var step = _tickCount > 5 ? LargeChange : SmallChange;
        _roundToPrecision = true;
        var text = step.ToString(CultureInfo.InvariantCulture);
        _precision = !text.ToLowerInvariant().Contains('e') && text.Contains('.') ? text.Split('.')[1].Length : 0;

        var widthChange = (_direction == Direction.Increase ? step : -step) * _density;
        if (beyond)
        {
            switch (_buttonType)
            {
                case ButtonType.BottomLeft:
                    MoveThumb(_leftEdge!, _middleThumb!, widthChange);
                    ReCalculateRangeSelected(true, false);
                    break;
                case ButtonType.TopRight:
                    MoveThumb(_middleThumb!, _rightEdge!, widthChange);
                    ReCalculateRangeSelected(false, true);
                    break;
                case ButtonType.Both:
                    MoveThumb(_leftEdge!, _rightEdge!, widthChange);
                    ReCalculateRangeSelected(true, true);
                    break;
            }
            CoerceLowerUpperValues();
        }

        _tickCount++;
    }

    private double EndPoint(ButtonType type, Direction direction)
    {
        var leftEdge = _leftEdge!.Bounds.Width;
        var rightEdge = _rightEdge!.Bounds.Width;
        if (direction == Direction.Increase)
        {
            if (type == ButtonType.BottomLeft || (type == ButtonType.Both && _isInsideRange))
                return leftEdge + _leftThumb!.Bounds.Width;
            return Bounds.Width - rightEdge;
        }

        if (type == ButtonType.BottomLeft || (type == ButtonType.Both && !_isInsideRange))
            return leftEdge;
        return Bounds.Width - rightEdge - _rightThumb!.Bounds.Width;
    }

    #endregion

    #region Tooltip and state

    // WPF lit the bar while the middle thumb was dragged or the track held, and while the pointer was over
    // the control, which a captured drag of either end keeps it. One pseudo-class covers all of them here,
    // so the look does not depend on whether Avalonia keeps :pointerover during a capture.
    private void UpdateActive() => PseudoClasses.Set(":active", _dragging != null || _trackHeld);

    private void ShowToolTip()
    {
        if (_toolTip == null || _dragging == null || AutoToolTipPlacement == RangeSliderToolTipPlacement.None)
            return;

        // MahApps centres the tip over the thumb for TopLeft and under it for BottomRight; the Popup's Top and
        // Bottom placements are exactly that, and it follows the thumb as it moves.
        _toolTip.PlacementTarget = _dragging;
        _toolTip.Placement = AutoToolTipPlacement == RangeSliderToolTipPlacement.TopLeft
            ? PlacementMode.Top
            : PlacementMode.Bottom;
        UpdateToolTipText();
        _toolTip.IsOpen = true;
        if (_toolTip.Child is { } child)
            _ = ToolTipFadeIn.RunAsync(child);
    }

    private void UpdateToolTipText()
    {
        if (_toolTipText == null || _dragging == null) return;
        _toolTipText.Text = _dragging == _leftThumb ? Format(LowerValue)
            : _dragging == _rightThumb ? Format(UpperValue)
            : Format(LowerValue) + " - " + Format(UpperValue);
    }

    // MahApps' GetToolTipNumber: the current culture's "N" format, so thousands are grouped.
    private string Format(double value)
    {
        var format = (NumberFormatInfo)NumberFormatInfo.CurrentInfo.Clone();
        format.NumberDecimalDigits = AutoToolTipPrecision;
        return value.ToString("N", format);
    }

    #endregion

    private enum ButtonType
    {
        BottomLeft,
        TopRight,
        Both
    }

    private enum Direction
    {
        Increase,
        Decrease
    }
}
