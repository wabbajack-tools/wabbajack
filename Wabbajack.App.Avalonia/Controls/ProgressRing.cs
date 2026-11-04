using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Wabbajack.App.Avalonia.Controls;

/// <summary>
/// Stands in for MahApps' ProgressRing: six dots that chase each other round a circle while
/// <see cref="IsActive" /> is set, in the control's <see cref="Foreground" />. MahApps draws the same thing
/// with a storyboard per dot; this draws it from one timer, eased so the dots bunch at the bottom and spread
/// at the top the way the Windows 8 ring does.
/// </summary>
public class ProgressRing : Control
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ProgressRing, bool>(nameof(IsActive), true);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<ProgressRing, IBrush?>(nameof(Foreground), Brushes.White);

    private const int Dots = 6;
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(3.2);

    private readonly DispatcherTimer _timer;
    private readonly DateTime _started = DateTime.UtcNow;

    public ProgressRing()
    {
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => InvalidateVisual());
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty || change.Property == IsVisibleProperty)
            UpdateTimer();
    }

    private void UpdateTimer()
    {
        if (IsActive && IsVisible && VisualRoot != null) _timer.Start();
        else _timer.Stop();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!IsActive || Foreground is null) return;

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var dot = size / 8;
        var radius = size / 2 - dot;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var t = (DateTime.UtcNow - _started).TotalSeconds / Period.TotalSeconds;

        for (var i = 0; i < Dots; i++)
        {
            // Each dot runs the same eased lap, a little behind the one before it.
            var phase = (t - i * 0.07) % 1.0;
            if (phase < 0) phase += 1;
            var eased = phase < 0.5 ? 2 * phase * phase : 1 - Math.Pow(-2 * phase + 2, 2) / 2;
            var angle = eased * 2 * Math.PI - Math.PI / 2;
            var p = new Point(centre.X + radius * Math.Cos(angle), centre.Y + radius * Math.Sin(angle));
            context.DrawEllipse(Foreground, null, p, dot / 2, dot / 2);
        }
    }
}
