using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Compiler
{
    public static class WabbajackToVortexCollection
    {
        public static string Serialize(ModList modList)
        {
            var obj = Build(modList);

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Serialize(obj, opts);
        }

        public static VortexCollection Build(ModList modList)
        {
            var listDomain = GetDomain(modList.GameType.ToString());

            var mods = new List<VortexMod>();
            var seen = new HashSet<(string domain, long modId, long fileId)>();

            foreach (var arch in modList.Archives ?? Array.Empty<Archive>())
            {
                if (arch.State is not Nexus nexusState)
                    continue;

                var modId = (long)nexusState.ModID;
                var fileId = (long)nexusState.FileID;
                if (modId <= 0 || fileId <= 0)
                    continue;

                var domain = listDomain;

                if (!seen.Add((domain, modId, fileId)))
                    continue;

                var name = string.IsNullOrWhiteSpace(nexusState.Name) ? $"Nexus {modId}" : nexusState.Name;
                var version = nexusState.Version ?? "";
                var author = nexusState.Author ?? "";
                var filesize = (long)arch.Size;
                mods.Add(new VortexMod
                {
                    Name = name,
                    Version = version,
                    Optional = false,
                    DomainName = domain,
                    Author = author,
                    Details = new VortexDetails { Category = "", Type = "" },
                    Phase = 0,
                    Source = new VortexSource
                    {
                        Type = "nexus",
                        ModId = modId,
                        FileId = fileId,
                        LogicalFilename = name,
                        UpdatePolicy = "prefer",
                        FileSize = filesize,
                    }
                });
            }

            return new VortexCollection
            {
                Info = new VortexInfo
                {
                    Author = modList.Author ?? "",
                    AuthorUrl = "",
                    Name = modList.Name ?? "",
                    Description = modList.Description ?? "",
                    InstallInstructions = "",
                    DomainName = listDomain,
                    GameVersions = new List<string>(),
                },
                Mods = mods,
                CollectionConfig = new Dictionary<string, object>(),
                LoadOrder = new List<object>(),
                ModRules = new List<object>(),
                Tools = new List<object>()
            };
        }

        public static string GetDomain(string? someName)
        {
            if (!string.IsNullOrWhiteSpace(someName) &&
                GameRegistry.TryGetByFuzzyName(someName.Trim(), out var meta) &&
                !string.IsNullOrWhiteSpace(meta.NexusName))
            {
                return meta.NexusName!;
            }

            return "site";
        }

        public sealed class VortexCollection
        {
            public VortexInfo Info { get; set; } = new();
            public List<VortexMod> Mods { get; set; } = new();
            public Dictionary<string, object> CollectionConfig { get; set; } = new();
            public List<object> LoadOrder { get; set; } = new();
            public List<object> ModRules { get; set; } = new();
            public List<object> Tools { get; set; } = new();
        }

        public sealed class VortexInfo
        {
            public string Author { get; set; } = "";
            public string AuthorUrl { get; set; } = "";
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string InstallInstructions { get; set; } = "";
            public string DomainName { get; set; } = "";
            public List<string> GameVersions { get; set; } = new();
        }

        public sealed class VortexMod
        {
            public string Name { get; set; } = "";
            public string Version { get; set; } = "";
            public bool Optional { get; set; }
            public string DomainName { get; set; } = "";
            public VortexSource Source { get; set; } = new();
            public string Author { get; set; } = "";
            public VortexDetails Details { get; set; } = new();
            public int Phase { get; set; }
        }

        public sealed class VortexSource
        {
            public string Type { get; set; } = "nexus";
            public long ModId { get; set; }
            public long FileId { get; set; }
            public string LogicalFilename { get; set; } = "";
            public string UpdatePolicy { get; set; } = "prefer";
            public long FileSize { get; set; }
        }

        public sealed class VortexDetails
        {
            public string Category { get; set; } = "";
            public string Type { get; set; } = "";
        }
    }
}
