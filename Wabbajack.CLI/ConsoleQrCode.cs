using System;
using System.Text;
using Net.Codecrete.QrCodeGenerator;

namespace Wabbajack.CLI;

/// <summary>
///     Draws a QR code in a terminal. Two module rows are packed into one character cell using half blocks,
///     because a cell is roughly twice as tall as it is wide and a scanner needs square modules.
/// </summary>
public static class ConsoleQrCode
{
    /// <summary>
    ///     The white margin a scanner needs to find the code. Four modules is what the specification asks
    ///     for; without it many phone cameras never lock on.
    /// </summary>
    private const int QuietZone = 4;

    private const char Both = '█'; // full block: dark above and below
    private const char Upper = '▀'; // upper half: dark above, light below
    private const char Lower = '▄'; // lower half: light above, dark below
    private const char Neither = ' ';

    /// <summary>
    ///     Renders <paramref name="text" /> as lines of block characters. Dark modules are drawn as blocks,
    ///     so the result scans on a light terminal as it stands and on a dark one with
    ///     <paramref name="invert" />, which swaps which half of each pair is drawn.
    /// </summary>
    public static string Render(string text, bool invert = false)
    {
        var qr = QrCode.EncodeText(text, QrCode.Ecc.Low);
        var size = qr.Size;
        var sb = new StringBuilder();

        // Step two rows at a time: the top row becomes the upper half of a character, the bottom row the
        // lower half. An odd-sized code leaves the final bottom row light, which the quiet zone covers.
        for (var y = -QuietZone; y < size + QuietZone; y += 2)
        {
            for (var x = -QuietZone; x < size + QuietZone; x++)
            {
                var top = Dark(qr, x, y, invert);
                var bottom = Dark(qr, x, y + 1, invert);
                sb.Append((top, bottom) switch
                {
                    (true, true) => Both,
                    (true, false) => Upper,
                    (false, true) => Lower,
                    _ => Neither
                });
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Whether the module at these coordinates should be drawn. Anything outside the code is quiet zone,
    ///     which is light. <see cref="QrCode.GetModule" /> already answers false out of bounds; the explicit
    ///     check keeps the intent readable.
    /// </summary>
    private static bool Dark(QrCode qr, int x, int y, bool invert)
    {
        var inside = x >= 0 && y >= 0 && x < qr.Size && y < qr.Size;
        var dark = inside && qr.GetModule(x, y);
        return invert ? !dark : dark;
    }
}
