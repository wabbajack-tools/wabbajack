namespace Wabbajack.Installer;

public sealed record ConfirmRequest(string Title, string Message, bool Severe = false,
    string? RequiredConfirmationText = null);
