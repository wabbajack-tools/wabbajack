using System;
using System.Linq;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs
{
    [JsonName("Manifest")]
    public class Manifest
    {
        public readonly string Name;
        public readonly Version Version;
        public readonly string Author;
        public readonly string Description;

        public readonly Game GameType;
        // Enum toString for better parsing in other software
        public string GameName;
        public readonly long DownloadSize;
        public readonly long InstallSize;

        public bool IsNSFW;

        public Archive[] Archives { get; set; }
        public InlineFile[] InlinedFiles { get; set; }

        public Manifest(ModList modlist)
        {
            Name = modlist.Name;
            Version = modlist.Version;
            Author = modlist.Author;
            Description = modlist.Description;

            GameType = modlist.GameType;
            GameName = GameType.ToString();

            DownloadSize = modlist.Archives.Sum(a => (long)a.Size);
            InstallSize = modlist.Directives.Sum(d => d.Size);

            IsNSFW = modlist.IsNSFW;

            // meta is being omitted due to it being useless and not very space friendly
            Archives = modlist.Archives.Select(a => new Archive
            {
                State = a.State,
                Hash = a.Hash,
                Name = a.Name,
                Size = a.Size,
            }).ToArray();

            InlinedFiles = modlist.Directives.OfType<InlineFile>().ToArray();
        }
    }
}