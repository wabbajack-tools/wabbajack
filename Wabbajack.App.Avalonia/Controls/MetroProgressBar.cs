using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Wabbajack.App.Avalonia.Controls;

/// <summary>
/// Stands in for MahApps' MetroProgressBar as the installer and preflight use it: the track in
/// <see cref="Background" />, the part done in <see cref="Foreground" /> from the left, square ends, no border.
/// Drawn directly rather than through ProgressBar, which sizes its indicator from the previous layout pass and
/// so leaves a bar whose value never changes after it is first laid out drawn empty. It asks for no size of its
/// own, so it takes whatever its slot gives it.
/// </summary>
public class MetroProgressBar : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<MetroProgressBar, double>(nameof(Value));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<MetroProgressBar, double>(nameof(Maximum), 1);

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<MetroProgressBar, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MetroProgressBar, IBrush?>(nameof(Foreground));

    static MetroProgressBar()
    {
        AffectsRender<MetroProgressBar>(ValueProperty, MaximumProperty, BackgroundProperty, ForegroundProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (Background is { } track) context.FillRectangle(track, bounds);

        var fraction = Maximum > 0 ? System.Math.Clamp(Value / Maximum, 0, 1) : 0;
        if (Foreground is { } fill && fraction > 0)
            context.FillRectangle(fill, new Rect(0, 0, bounds.Width * fraction, bounds.Height));
    }
}
