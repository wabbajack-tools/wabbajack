using System.Text.RegularExpressions;
using Wabbajack.Common;

namespace Wabbajack.Lib.NexusApi
{
    public sealed class NexusApiUtils
    {
        public static string ConvertGameName(string gameName)
        {
            if (Regex.IsMatch(gameName, @"^[^a-z\s]+\.[^a-z\s]+$"))
                return gameName;
            return GameRegistry.GetByMO2ArchiveName(gameName)?.NexusName ?? gameName.ToLower();
        }

        public static string GetModURL(Game game, string argModId)
        {
            return $"https://nexusmods.com/{GameRegistry.Games[game].NexusName}/mods/{argModId}";
        }

        public static string FixupSummary(string argSummary)
        {
            if (argSummary != null)
            {
                return argSummary.Replace("&#39;", "'")
                                 .Replace("<br/>", "\n\n")
                                 .Replace("<br />", "\n\n")
                                 .Replace("&#33;", "!");
            }

            return argSummary;
        }
    }
}
