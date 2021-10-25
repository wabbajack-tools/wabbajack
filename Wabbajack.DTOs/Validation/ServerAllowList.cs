using System;

namespace Wabbajack.DTOs.Validation;

public class ServerAllowList
{
    public string[] AllowedPrefixes = Array.Empty<string>();
    public string[] GoogleIDs = Array.Empty<string>();
}