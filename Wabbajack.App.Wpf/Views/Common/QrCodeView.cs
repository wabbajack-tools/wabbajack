using System;
using System.Windows;
using System.Windows.Media;
using Net.Codecrete.QrCodeGenerator;

namespace Wabbajack;

/// <summary>
///     Draws a string as a scannable QR code.
///     <para>
///         The modules are drawn as rectangles rather than rasterised into a bitmap: the same element has to
///         look right at whatever size the pane gives it, and a bitmap scaled to fit is exactly the blur a
///         phone camera fails to lock onto. The module size is rounded down to a whole device pixel and the
///         result is centred in what is left over, so every module is the same size and every edge lands on a
///         pixel boundary; <see cref="EdgeMode.Aliased" /> then keeps those edges hard instead of
///         antialiasing each one into a grey fringe.
///     </para>
///     <para>
///         Black on white regardless of the theme, which is the one place in the app that is deliberately not
///         themed. A scanner reads contrast, and a code drawn in the app's own foreground on its own
///         background is a code that does not scan.
///     </para>
/// </summary>
public class QrCodeView : FrameworkElement
{
    /// <summary>
    ///     The light border a scanner needs to find the code at all. Four modules is what the specification
    ///     asks for, and without it many phone cameras never lock on.
    /// </summary>
    private const int QuietZone = 4;

    private static readonly Brush Light = Brushes.White;
    private static readonly Brush Dark = Brushes.Black;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(QrCodeView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnTextChanged));

    private QrCode? _code;

    public QrCodeView()
    {
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        SnapsToDevicePixels = true;
    }

    /// <summary>What the code encodes. Null or empty draws the empty card rather than an empty code.</summary>
    public string? Text
    {
        get => (string?) GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (QrCodeView) d;

        // Error correction stays Low on purpose: a Steam challenge URL is long, and every step up in
        // correction is another module across, which at a fixed pane size means smaller modules to scan.
        view._code = e.NewValue is string text && !string.IsNullOrEmpty(text)
            ? QrCode.EncodeText(text, QrCode.Ecc.Low)
            : null;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = RenderSize.Width;
        var height = RenderSize.Height;
        if (width <= 0 || height <= 0) return;

        drawingContext.DrawRectangle(Light, null, new Rect(0, 0, width, height));
        if (_code == null) return;

        var across = _code.Size + 2 * QuietZone;
        var module = Math.Floor(Math.Min(width, height) / across);
        if (module < 1) return;

        var drawn = module * across;
        var left = Math.Floor((width - drawn) / 2) + QuietZone * module;
        var top = Math.Floor((height - drawn) / 2) + QuietZone * module;

        for (var y = 0; y < _code.Size; y++)
        for (var x = 0; x < _code.Size; x++)
        {
            if (!_code.GetModule(x, y)) continue;
            drawingContext.DrawRectangle(Dark, null,
                new Rect(left + x * module, top + y * module, module, module));
        }
    }
}
