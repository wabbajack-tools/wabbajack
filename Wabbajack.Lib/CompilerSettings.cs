using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib
{
    public class CompilerSettings
    {
        public const string FileName = "compiler_settings.json";

        public static async Task<CompilerSettings> Load(AbsolutePath folder)
        {
            var path = folder.Combine(FileName);
            return !path.IsFile ? new CompilerSettings() : path.FromJson<CompilerSettings>();
        }

        public Game[] IncludedGames { get; set; } = new Game[0];
        public string[] OtherProfiles { get; set; } = new string[0];
    }
}
