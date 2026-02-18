using System;

namespace Wabbajack.App.Avalonia.Models;

public class ValidationResult
{
    public static readonly ValidationResult Success = new(true);
    public static readonly ValidationResult Failure = new(false);

    public bool Succeeded { get; }
    public bool Failed => !Succeeded;
    public string Reason { get; }
    public Exception? Exception { get; }

    public ValidationResult(bool succeeded, string? reason = null, Exception? ex = null)
    {
        Succeeded = succeeded;
        Reason = reason ?? string.Empty;
        Exception = ex;
    }
}
