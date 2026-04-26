using System.Collections.Generic;

namespace Wabbajack.DTOs.GitHub;

public static class PathNames
{
    public static Dictionary<List, string> FromList = new()
    {
        {List.CI, "ci_lists.json"},
        {List.Unlisted, "unlisted_modlists.json"},
        {List.Utility, "utility_modlists.json"},
        {List.Published, "modlists.json"}
    };
}