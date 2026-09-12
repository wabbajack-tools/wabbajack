using System.Security.Cryptography;

namespace Wabbajack.Paths.IO;

/// <summary>
///     Generates short random names for temporary files and folders.
/// </summary>
public static class RandomName
{
    /// <summary>
    ///     Names are kept short to reduce the path footprint of temporary files, so that deeply nested
    ///     mod folders stay clear of the <c>MAX_PATH</c> limit.
    /// </summary>
    public const int DefaultLength = 16;

    /// <summary>
    ///     Windows paths and <see cref="AbsolutePath" /> both compare case-insensitively, so the alphabet is
    ///     restricted to a single case. A mixed-case alphabet would make two names that the filesystem treats
    ///     as the same path look distinct here.
    /// </summary>
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    ///     Returns a random name of <paramref name="length" /> characters. Safe to call concurrently from any
    ///     number of threads.
    /// </summary>
    public static string Next(int length = DefaultLength)
    {
        return RandomNumberGenerator.GetString(Alphabet, length);
    }
}
