using System;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;

namespace Wabbajack.GameFinder.Paths.Utilities;

internal static class EnumeratorHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchesPattern(string expression, ReadOnlySpan<char> name, MatchType matchType)
    {
        switch (matchType)
        {
            case MatchType.Win32:
                return FileSystemName.MatchesWin32Expression(expression.AsSpan(), name);
            case MatchType.Simple:
                return FileSystemName.MatchesSimpleExpression(expression.AsSpan(), name);
            default:
                ThrowHelpers.ArgumentOutOfRange(nameof(matchType));
                return false;
        }
    }
}
