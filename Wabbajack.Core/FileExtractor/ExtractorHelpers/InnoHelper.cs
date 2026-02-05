using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Wabbajack.FileExtractor.ExtractorHelpers;

internal static class InnoHelper
{
    private const int B = 1;
    private const int KiB = 1024;
    private const int MiB = KiB * 1024;
    private const int GiB = MiB * 1024;

    /* 
     * InnoExtract apparently supports these sizes, but I can't imagine these files actually existing in the wild.
     * It just makes things complicated right now, so don't support them.

     * private const int TiB = GiB * 1024;
     * private const int PiB = TiB * 1024;
     * private const ulong EiB = PiB * 1024;
     * private const ulong ZiB = EiB * 1024;
     * private const ulong YiB = ZiB * 1024;
    */

    public static int GetExtractedFileSize(string line)
    {
        int length = line.Length;
        int i = length - 1;
        while (i >= 0 && line[i] != ')') i--;

        int j = i;

        while (j >= 0 && line[j] != ' ') j--;

        string unit = line.Substring(j + 1, i - j - 1);

        i = j - 1;
        while (i >= 0 && line[i] != '(') i--;

        double size = double.Parse(line.Substring(i + 1, j - i), CultureInfo.InvariantCulture);

        long multiplier = unit switch
        {
            "B" => B,
            "KiB" => KiB,
            "MiB" => MiB,
            "GiB" => GiB,
            _ => throw new NotSupportedException($"Unknown size unit: {unit}")
        };

        return (int)(size * multiplier);
    }

}
