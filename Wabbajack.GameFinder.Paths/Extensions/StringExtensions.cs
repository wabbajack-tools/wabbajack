using System;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.Paths.Extensions;

/// <summary>
/// Path related extensions tied to strings.
/// </summary>
[PublicAPI]
public static class StringExtensions
{
    /// <summary>
    /// Converts an existing path represented as a string to a <see cref="RelativePath"/>.
    /// </summary>
    [Obsolete($"Use implicit conversion or {nameof(RelativePath.FromUnsanitizedInput)}")]
    public static RelativePath ToRelativePath(this string s) => s;
}
