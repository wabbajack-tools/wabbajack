using System;

namespace Wabbajack.DTOs.Validation;

/// <summary>
///     Bans.yml in opt-out-lists: hosts a modlist may not take files from at all, whether automated or by
///     hand. Unlike the allow-list, which only decides what Wabbajack fetches by itself, a list that names one
///     of these is refused.
/// </summary>
public class ServerBanList
{
    public string[] Bans = Array.Empty<string>();
}
