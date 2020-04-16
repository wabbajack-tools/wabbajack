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
        public string Name;
        public Version Version;
        public string Author;
        public string Description;

        public Game GameType;
        // Enum toString for better parsing in other software
        public string GameName;

        public ModManager ModManager;
        // Enum toString for better parsing in other software
        public string ModManagerName;

        public long DownloadSize;
        public long InstallSize;

        public List<Archive> Archives;

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

            // meta is being omitted due to it being useless and not very space friendly
            Archives = modlist.Archives.Select(a => new Archive(a.State)
            {
                Hash = a.Hash,
                Name = a.Name,
                Size = a.Size,
            }).ToList();
        }
    }
}
