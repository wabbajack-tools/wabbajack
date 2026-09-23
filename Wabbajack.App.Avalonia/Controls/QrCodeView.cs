using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Net.Codecrete.QrCodeGenerator;

namespace Wabbajack.App.Avalonia.Controls;

/// <summary>
///     Draws a string as a scannable QR code, as the WPF app's QrCodeView did.
///     <para>
///         The modules are drawn as rectangles rather than a scaled bitmap, each a whole number of device
///         pixels, with the code centred in what is left over and edges drawn aliased, so a phone camera sees
///         hard edges at any size and any DPI.
///     </para>
///     <para>
///         Black on white regardless of the theme: a scanner reads contrast.
///     </para>
/// </summary>
public class QrCodeView : Control
{
    /// <summary>The light border a scanner needs to find the code at all; four modules is the specification's.</summary>
    private const int QuietZone = 4;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<QrCodeView, string?>(nameof(Text));

    private QrCode? _code;

    static QrCodeView()
    {
        AffectsRender<QrCodeView>(TextProperty);
    }

    public QrCodeView()
    {
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    /// <summary>What the code encodes. Null or empty draws the empty card.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TextProperty) return;

        if (string.IsNullOrEmpty(Text))
        {
            _code = null;
            return;
        }

        try
        {
            // Low error correction on purpose: a Steam challenge URL is long, and every step up is more
            // modules across, which at a fixed size means smaller modules to scan.
            _code = QrCode.EncodeText(Text, QrCode.Ecc.Low);
        }
        catch (Exception)
        {
            // Only for a string too long to encode; the pane has other ways out, so draw the empty card.
            _code = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        context.FillRectangle(Brushes.White, new Rect(0, 0, width, height));
        if (_code == null) return;

        // Worked out in device pixels, because a whole number of DIPs is not a whole number of pixels at
        // 125% or 150%, and a module edge landing mid-pixel softens the code.
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (scale <= 0) scale = 1;

        var across = _code.Size + 2 * QuietZone;
        var pixels = Math.Floor(Math.Min(width, height) * scale / across);
        if (pixels < 1) return;

        var module = pixels / scale;
        var drawn = module * across;
        var left = Math.Floor((width - drawn) / 2 * scale) / scale + QuietZone * module;
        var top = Math.Floor((height - drawn) / 2 * scale) / scale + QuietZone * module;

        for (var y = 0; y < _code.Size; y++)
        for (var x = 0; x < _code.Size; x++)
        {
            if (!_code.GetModule(x, y)) continue;
            context.FillRectangle(Brushes.Black, new Rect(left + x * module, top + y * module, module, module));
        }
    }
}
