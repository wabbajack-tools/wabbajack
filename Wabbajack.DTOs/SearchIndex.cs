using System.Collections.Generic;

namespace Wabbajack.DTOs;

public class SearchIndex
{
    /// <summary>
    /// All unique mods across all modlists
    /// </summary>
    public HashSet<string> AllMods { get; set; }
    
    /// <summary>
    /// All mods included per modlist (key: machineURL)
    /// </summary>
    public Dictionary<string, HashSet<string>> ModsPerList { get; set; }
}