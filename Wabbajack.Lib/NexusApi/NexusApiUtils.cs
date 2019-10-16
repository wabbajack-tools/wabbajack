using Wabbajack.Common;

namespace Wabbajack.Lib.NexusApi
{
    public sealed class NexusApiUtils
    {
        public static string ConvertGameName(string gameName)
        {
            return GameRegistry.GetByMO2ArchiveName(gameName)?.NexusName ?? gameName.ToLower();
        }

        public static string GetModURL(string argGameName, string argModId)
        {
            return $"https://nexusmods.com/{ConvertGameName(argGameName)}/mods/{argModId}";
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
