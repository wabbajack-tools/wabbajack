using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.Lib
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

        public readonly ModManager ModManager;
        // Enum toString for better parsing in other software
        public string ModManagerName;

        public readonly long DownloadSize;
        public readonly long InstallSize;

        public bool IsNSFW;

        public readonly List<Archive> Archives;
        public readonly List<InlineFile> InlinedFiles;

        public Manifest(ModList modlist)
        {
            Name = modlist.Name;
            Version = modlist.Version;
            Author = modlist.Author;
            Description = modlist.Description;

            GameType = modlist.GameType;
            GameName = GameType.ToString();

            ModManager = modlist.ModManager;
            ModManagerName = ModManager.ToString();

            DownloadSize = modlist.DownloadSize;
            InstallSize = modlist.InstallSize;

            IsNSFW = modlist.IsNSFW;

            // meta is being omitted due to it being useless and not very space friendly
            Archives = modlist.Archives.Select(a => new Archive(a.State)
            {
                Hash = a.Hash,
                Name = a.Name,
                Size = a.Size,
            }).ToList();

            InlinedFiles = modlist.Directives.OfType<InlineFile>().ToList();
        }
    }
}
