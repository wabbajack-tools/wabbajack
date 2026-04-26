namespace Wabbajack.DTOs.Logins;

/// <summary>
///     A HTTP cookie (used for login management)
/// </summary>
public class Cookie
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}