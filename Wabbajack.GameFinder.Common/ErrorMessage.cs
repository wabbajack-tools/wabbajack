using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.Common;

/// <summary>
/// Represents a generic error.
/// </summary>
[PublicAPI]
[DebuggerDisplay("{Message}")]
public readonly struct ErrorMessage
{
    /// <summary>
    /// The error message.
    /// </summary>
    public readonly string Message;

    /// <summary>
    /// Constructor taking a message.
    /// </summary>
    /// <param name="message"></param>
    public ErrorMessage(string message)
    {
        Message = message;
    }

    /// <summary>
    /// Constructor taking an exception.
    /// </summary>
    /// <param name="e"></param>
    public ErrorMessage(Exception e)
    {
        Message = e.ToString();
    }

    /// <summary>
    /// Constructor taking an exception and a message.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="message"></param>
    public ErrorMessage(Exception e, string message)
    {
        Message = $"{message}:\n{e}";
    }

    /// <inheritdoc/>
    public override string ToString() => Message;

    /// <summary>
    /// Converts <see cref="ErrorMessage"/> to a <see cref="string"/>.
    /// </summary>
    public static explicit operator string(ErrorMessage error) => error.Message;

    /// <summary>
    /// Creates a new <see cref="ErrorMessage"/> from a <see cref="string"/>.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static implicit operator ErrorMessage(string message) => new(message);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj switch
        {
            null => false,
            string s => string.Equals(Message, s, StringComparison.InvariantCulture),
            ErrorMessage errorMessage => string.Equals(Message, errorMessage.Message, StringComparison.InvariantCulture),
            _ => false
        };
    }

    /// <inheritdoc/>
    public override int GetHashCode() => Message.GetHashCode(StringComparison.InvariantCulture);
}
