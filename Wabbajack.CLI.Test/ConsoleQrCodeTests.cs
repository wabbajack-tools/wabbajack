using System;
using System.Linq;
using Net.Codecrete.QrCodeGenerator;
using Xunit;

namespace Wabbajack.CLI.Test;

/// <summary>
///     The encoder is the library's business; what is tested here is the drawing. A code that is offset by a
///     module, packed in the wrong order or drawn inverted still looks like a QR code to a reader and does
///     not scan, so the rendering is read back into a grid and compared against the modules it came from.
/// </summary>
public class ConsoleQrCodeTests
{
    private const string Url = "https://s.team/q/1/12270599264471307119";
    private const int QuietZone = 4;

    /// <summary>
    ///     Turns the rendered block characters back into the module grid they represent. Each character
    ///     carries two rows: the upper half and the lower half.
    /// </summary>
    private static bool[,] ReadBack(string rendered, int size, bool invert)
    {
        var lines = rendered.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var span = size + QuietZone * 2;
        var grid = new bool[span, span];

        for (var row = 0; row < lines.Length; row++)
        for (var x = 0; x < lines[row].Length; x++)
        {
            var c = lines[row][x];
            var top = c is '█' or '▀';
            var bottom = c is '█' or '▄';
            if (invert)
            {
                top = !top;
                bottom = !bottom;
            }

            var y = row * 2;
            if (y < span) grid[y, x] = top;
            if (y + 1 < span) grid[y + 1, x] = bottom;
        }

        return grid;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheDrawingMatchesTheModulesItCameFrom(bool invert)
    {
        var qr = QrCode.EncodeText(Url, QrCode.Ecc.Low);
        var grid = ReadBack(ConsoleQrCode.Render(Url, invert), qr.Size, invert);

        for (var y = 0; y < qr.Size; y++)
        for (var x = 0; x < qr.Size; x++)
            Assert.Equal(qr.GetModule(x, y), grid[y + QuietZone, x + QuietZone]);
    }

    /// <summary>
    ///     Without the margin a phone camera often never locks on, and it is the kind of thing that is easy
    ///     to lose while tidying the loops.
    /// </summary>
    [Fact]
    public void TheCodeIsSurroundedByAQuietZone()
    {
        var qr = QrCode.EncodeText(Url, QrCode.Ecc.Low);
        var grid = ReadBack(ConsoleQrCode.Render(Url), qr.Size, false);
        var span = qr.Size + QuietZone * 2;

        for (var y = 0; y < span; y++)
        for (var x = 0; x < span; x++)
        {
            var insideCode = x >= QuietZone && x < qr.Size + QuietZone &&
                             y >= QuietZone && y < qr.Size + QuietZone;
            if (!insideCode) Assert.False(grid[y, x], $"module at {x},{y} is in the quiet zone but is dark");
        }
    }

    [Fact]
    public void EveryLineIsTheSameWidth()
    {
        var lines = ConsoleQrCode.Render(Url).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length > 0);
        Assert.Single(lines.Select(l => l.Length).Distinct());
    }

    /// <summary>
    ///     Steam rotates the challenge URL, so the renderer is called repeatedly with whatever it sends.
    /// </summary>
    [Fact]
    public void AnEmptyOrLongTextStillRenders()
    {
        Assert.NotEmpty(ConsoleQrCode.Render(""));
        Assert.NotEmpty(ConsoleQrCode.Render(new string('x', 400)));
    }
}
