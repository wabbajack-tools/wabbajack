using Avalonia;
using Avalonia.Controls;

namespace Wabbajack.App.Avalonia.Controls;

/// <summary>
/// A box that pulses to ask for attention: its border and fill swing between two colours every 1.5 seconds
/// while it is not under the pointer, in teal normally and in red when <see cref="Failure" /> is set. The
/// look is in Controls.axaml.
/// </summary>
public class AttentionBorder : ContentControl
{
    public static readonly StyledProperty<bool> FailureProperty =
        AvaloniaProperty.Register<AttentionBorder, bool>(nameof(Failure));

    public bool Failure
    {
        get => GetValue(FailureProperty);
        set => SetValue(FailureProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FailureProperty)
            PseudoClasses.Set(":failure", Failure);
    }
}
